using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using ConstantesLibrary;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Threading;

namespace WorkerRole
{
	public class WorkerRole : RoleEntryPoint
	{
		public override void Run()
		{
			try {
				try {
					BusinessProcessLibrary.Configuration.DownloadGdal();
					BusinessProcessLibrary.Configuration.ConfGdal();
				}
				catch (Exception e) {
					Trace.WriteLine("Erreur critique: Fail to Download Gdal ?\n WorkerRole:" + e, EnumUtils.AsString(LogsTypes.DevError));
					return;
				}

				// On lance les Threads de travail, ...
				int threadNumber;
				string threadNumberString = RoleEnvironment.GetConfigurationSettingValue("ThreadNumber");
				if (!int.TryParse(threadNumberString, NumberStyles.Integer, CultureInfo.InvariantCulture, out threadNumber))	{
					Trace.WriteLine("Fail to find the number of thread, fail to convert ThreadNumber: \"" + threadNumberString + "\"",
									EnumUtils.AsString(LogsTypes.DevError)); KillDeployment();
				}

				Thread[] threads = StartThreads(threadNumber);

				// puis on commence à les surveiller.
				while (true) {
					try {
						Watching(threads);
					}
					catch (Exception e) {
						Trace.WriteLine("I'm ill, look that " + e, EnumUtils.AsString(LogsTypes.Supervision));
					}
				}
			}
			catch (Exception e)	{
				Trace.WriteLine("Erreur critique: WorkerRole:" + e, EnumUtils.AsString(LogsTypes.DevError));
			}
			//function (have to) never return !
		}

		public override bool OnStart()
		{
			ServicePointManager.DefaultConnectionLimit = 12;
			return base.OnStart();
		}

		
		private static Thread[] StartThreads(int threadNumber)
		{
			Thread[] threads = new Thread[threadNumber];
			for (int i = 0; i < threadNumber; i++)
			{
				Worker workerObject = new Worker(i.ToString(CultureInfo.InvariantCulture));
				threads[i] = new Thread(workerObject.Run) { IsBackground = true, Name = i.ToString(CultureInfo.InvariantCulture) };
				threads[i].Start();
				while (!threads[i].IsAlive) { }
				Thread.Sleep(1);
			}

			return threads;
		}

		private static void Watching(Thread[] threads)
		{
			// TODO : surveiller pour de vrai
			// can use ShouldStop
			Trace.WriteLine("I'm watching U ! You are "
				+ ((threads.Length != 1) ? threads.Length.ToString(CultureInfo.InvariantCulture) : "Lonely ")
				+ " !!"
				, EnumUtils.AsString(LogsTypes.Supervision));
			// 2h
			Thread.Sleep(2 * 60 * 60 * 1000);
		}

		private void KillDeployment()
		{
			Trace.WriteLine("This deployment (" + RoleEnvironment.DeploymentId + ") will be kill.", EnumUtils.AsString(LogsTypes.Information));
			// TODO : implement this
			throw new NotImplementedException();
		}

	}
}

