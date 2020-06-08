using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel.AppService;

namespace DokanNet.Tardigrade.UWP.SysTray
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            
            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("*****************************");
            //Console.WriteLine("**** Classic desktop app ****");
            //Console.WriteLine("*****************************");
            //Console.ReadLine();
            Mutex mutex = null;
            if (!Mutex.TryOpenExisting("TardigradeSystrayExtensionMutex", out mutex))
            {
                mutex = new Mutex(false, "TardigradeSystrayExtensionMutex");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SystrayApplicationContext());
                mutex.Close();
            }
        }

        
    }
}
