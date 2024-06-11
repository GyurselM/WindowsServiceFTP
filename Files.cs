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


namespace WindowsServiceFTP
{
    partial class Files : ServiceBase
    {
        bool available = false;
        public Files()
        {
            InitializeComponent();
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

                using (EventLog eventLog = new EventLog("Application"))
                {
                    eventLog.Source = "Application";
                    eventLog.WriteEntry("Se inició el proceso de copiado", EventLogEntryType.Information);
                }
                
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

                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        eventLog.Source = "Application";
                        eventLog.WriteEntry("Transferencia de archivos completada", EventLogEntryType.Information);
                    }
                }

                using (EventLog eventLog = new EventLog("Application"))
                {
                    eventLog.Source = "Application";
                    eventLog.WriteEntry("Ha finalizado el proceso de copiado", EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                using (EventLog eventLog = new EventLog("Application"))
                {
                    eventLog.Source = "Application";
                    eventLog.WriteEntry("Error en la transferencia de archivos: " + ex.Message, EventLogEntryType.Error);
                }
            }

            available = false;
        }
    }
}