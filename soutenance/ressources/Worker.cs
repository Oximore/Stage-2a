using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.WindowsAzure.ServiceRuntime;
using ToolsLibrary;
using BusinessProcessLibrary;
using ConstantesLibrary;

namespace WorkerRole
{
	/// <summary>
	/// Pas de notion de queue ici. 
	/// C'est juste un thread qui s'occupe d'apeller les tâches une par une.
	/// C'est elles qui doivent trouver leur travail toutes seules.
	/// Elles doivent (et sont l'intégralitée des classe qui ) héritées de la classe abstraite BusinessProcess.
	/// </summary>
	/// <remarks>
	/// A chaque Worker correspond un espace de travail WorkDirectoryName.
	/// 
	/// </remarks>
	public class Worker
	{
		private string Name { get; set; }
		private string WorkDirectoryName { get; set; }
		private Sleeper CustomSleeper { get; set; }
		private ulong CountLoop { get; set; }
		private readonly List<IBusinessProcess> _employees;

		public Worker(string name)
		{
			Name = name;
			LocalResource azureLocalResource = RoleEnvironment.GetLocalResource("LocalStorage");
			WorkDirectoryName = Path.Combine(azureLocalResource.RootPath, "WD" + name);

			/*** Init ***/
			// On instancie un objet de chaque classe héritant de BusinessProcess
			_employees = new List<IBusinessProcess>();
			Object[] args = new object[] { WorkDirectoryName };
			foreach (Type instance in
					typeof(BusinessProcess).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BusinessProcess))))
			{
				_employees.Add(Activator.CreateInstance(instance, args) as IBusinessProcess);
			}

			//// TODO : find a good time
			int maximumTimeToSleepInSeconde;
			if (!int.TryParse(RoleEnvironment.GetConfigurationSettingValue("MaximumSecondesTimeSleep"),
				out maximumTimeToSleepInSeconde))
				maximumTimeToSleepInSeconde = 2 * 60 * 60; // 2 heures
			CustomSleeper = new Sleeper(2, maximumTimeToSleepInSeconde);
			CountLoop = 0;
		}

		public void Run() {
			
			// On init chaque "employé"
			foreach (IBusinessProcess businessProcess in _employees) {
				businessProcess.Init();
			}

			while (true) {
				try	{
					for (int i = 0; i < _employees.Count; i++) {
						// On fait travailler chaque employé à tour de role, tant qu'ils ont du travail
						if (!WorkerRole.ShouldStop && _employees[i].Work())	{
							// i=-1 : pour se concentrer sur une tâche en particulier
							i = -1;
							CustomSleeper.Reset();
						}
					}
					// If I have not to work or no work
					Sleep();
				}
				catch (Exception e) {
					LogException(e, LogsTypes.DevFailure);
					// 2 minutes au bagne, il n'y a pas a en avoir ici.
					Thread.Sleep(2 * 60 * 1000);
				}
			}
			//function (have to) never return !
		}
		


		private void LogException(Exception e, LogsTypes typeOfError)
		{
			Trace.WriteLine(
				String.Format(@"Error on thread loop of thread {0} instance {1} " + e,
				Name, RoleEnvironment.CurrentRoleInstance),
				EnumUtils.AsString(typeOfError));
		}
		protected virtual void Sleep()
		{
			CustomSleeper.Increment();
			Trace.WriteLine("Thread " + Name + ", no more message, sleep " + CustomSleeper.TimeToSleep + "s", EnumUtils.AsString(LogsTypes.Verbatile));
			CustomSleeper.Sleep();
		}
	}
}

