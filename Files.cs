using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using WinSCP;
using log4net;
using log4net.Config;
using System.Runtime.CompilerServices;


namespace WindowsServiceFTP
{
    partial class Files : ServiceBase
    {
        private static readonly ILog Log = Logs.GetLogger();

        bool available = false;
        public Files()
        {
            InitializeComponent();
            XmlConfigurator.Configure(new System.IO.FileInfo("App.config"));
        }

        protected override void OnStart(string[] args)
        {
            stLapso.Start();
        }

        protected override void OnStop()
        {
            stLapso.Stop();
        }

        private void stLapso_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (available) return;

            try
            {
                available = true;// hacer esperar ya que se esta ejecutando

                Log.Info($"Se inició el proceso de copiado");
              
                // Servicio FTP
                string ftpOriginRoute = ConfigurationSettings.AppSettings["ftpOriginRoute"].ToString();
                string ftpDestinationRoute = ConfigurationSettings.AppSettings["ftpDestinationRoute"].ToString();
                string ftpUsername = ConfigurationManager.AppSettings["ftpUsername"].ToString();
                string ftpPassword = ConfigurationManager.AppSettings["ftpPassword"].ToString();
                string sshHostKeyFingerprint = ConfigurationManager.AppSettings["sshHostKeyFingerprint"];
                int portOrigin = Convert.ToInt32(ConfigurationManager.AppSettings["portOrigin"]);
                int portDestination = Convert.ToInt32(ConfigurationManager.AppSettings["portDestination"]);

                // Extraer el hostname del origen
                Uri originUri = new Uri(ftpOriginRoute);
                string originHostName = originUri.Host;
                string originPath = originUri.AbsolutePath;

                // Extraer el hostname del destino
                Uri destinationUri = new Uri(ftpDestinationRoute);
                string destinationHostName = destinationUri.Host;
                string destinationPath = destinationUri.AbsolutePath;

                // Establecer opciones de sesión para el origen
                SessionOptions sessionOptionsOrigin = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = originHostName,
                    UserName = ftpUsername,
                    Password = ftpPassword,
                    SshHostKeyFingerprint = sshHostKeyFingerprint,
                    PortNumber = portOrigin,
                };

                // Establecer opciones de sesión para el destino
                SessionOptions sessionOptionsDestination = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = destinationHostName,
                    UserName = ftpUsername,
                    Password = ftpPassword,
                    SshHostKeyFingerprint = sshHostKeyFingerprint,
                    PortNumber = portDestination,
                };

                using (Session session = new Session())
                {
                    TransferOptions transferOptions = new TransferOptions
                    {
                        TransferMode = TransferMode.Binary
                    };
                    //Conectar
                    session.Open(sessionOptionsOrigin);
                    //Creacion de tabla temporal
                    string tempLocalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SftpTransfer");
                    Directory.CreateDirectory(tempLocalPath);

                    //Descarga los archivos a una carpeta temporal
                    session.GetFiles(originPath + "*", tempLocalPath + "\\*", true, transferOptions).Check();

                    // Conectar al destinatario
                    session.Close();
                    session.Open(sessionOptionsDestination);

                    // Subir archivo
                    session.PutFiles(tempLocalPath + "\\*", destinationPath + "/", true, transferOptions).Check();
                    
                    //Borrar carpeta temporal
                    Directory.Delete(tempLocalPath, true);

                    Log.Info("Transferencia de archivos completada");

                }

                Log.Info("Ha finalizado el proceso de copiado");

            }
            catch (Exception ex)
            {
                Log.Error($"Error en la transferencia de archivos: {ex.Message}");
            }

            available = false;
        }
    }
    // Clases Log
    /*
     * Si contamos con una versión de .Net framework mayor o igual a 4.6 
     * podemos crear una clase logs y hacer referencia de la siguiente manera, ->  private static readonly ILog Log = Logs.GetLogger();
     * esta manera nos brinda mayor velocidad y rendimiento en aplicaciones que requiera un volumen grande de logs.
     * 
     * Si nuestra version es inferior pondremos lo sieguiente en nuestra class del proyecto:
     * private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
     * 
     */
    class Logs
    {
        public static ILog GetLogger()
        {
            return LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        /*public static ILog GetLogger([CallerFilePath] string filename = "")
        {
            return LogManager.GetLogger(filename);
        }*/
    }
}