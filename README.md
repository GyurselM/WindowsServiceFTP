# WindowsServiceFTP

### License

This project is licensed under the Creative Commons Attribution-NoDerivatives 4.0 International License. You can view the full license [here](./LICENSE.md).

Esta trabajo lo realice en mis practicas con MRW donde tenia que crear un servicio de windows donde tenia que pasar archivos de un servidor a otro utilizando FTP
y creando mi propio log para registrar todo lo que se estaba moviendo tanto en local como en BBDD (SQL Server).	
Este es el achivo principal la cual me permite pasar archivos de un sitio a otro.

```csharp
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
                    var transferResult = session.GetFiles(originPath + "*", tempLocalPath + "\\*", true, transferOptions);
                    transferResult.Check();
                    // Conectar al destinatario
                    session.Close();
                    session.Open(sessionOptionsDestination);

                    // Subir archivo
                    transferResult = session.PutFiles(tempLocalPath + "\\*", destinationPath + "/", true, transferOptions);
                    transferResult.Check();

                    foreach (var transfer in transferResult.Transfers)
                    {
                        string fileName = Path.GetFileName(transfer.FileName);
                        Log.Info($"Archivo enviado: {fileName}");
                    }

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
```

Esta otra, es la clase Log la cual lo llamo arriba de todo para que poder utilizar el log en todo mi codigo.

```csharp
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
    }
```

A continuacion esta el App.config que es donde he creado el log tanto localmente como en BBDD tambien al final he creado un AppSettings para poder poner todo dato relevante como:
- Ruta de los FTP, añadiendo tanto el origen y el destino de los FTPs. eje: "D:\Origen\" y "D:\Destino\"
- ftpUsername y ftpPassword
- HuellaDigital (Que es basicanebte un ssh-rsa para poder hacer lo todo de manera segura)
- Los Puertos
  
Basicamente he creado esto para que en un futuro si se necesita un cambio de desdino, usuario, contraseña o pueto, es tan facil como acceder aqui y hacer todo el cambio

```xml
﻿<?xml version="1.0" encoding="utf-8" ?>
<configuration>
	<configSections>
		<section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler,log4net" />
	</configSections>
	<startup>
		<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
	</startup>

	<!--Log4net-->
	<log4net>
		<appender name="MainFileAppender" type="log4net.Appender.FileAppender">
			<file type="log4net.Util.PatternString" value="Logs/logfile_%date{yyyy-MM-dd}.log" />
			<encoding value="utf-8" />
			<appendToFile value="true" />
			<layout type="log4net.Layout.PatternLayout">
				<conversionPattern value="%date > [%logger]{%method} > %level:: %message%n" />
			</layout>
		</appender>
		<!-- Conexion a BBDD -->
		<appender name="AdoNetAppender" type="log4net.Appender.AdoNetAppender">
			<bufferSize value="1" />
			<connectionType value="System.Data.SqlClient.SqlConnection, System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" />
			<connectionString value="Data Source=XXXXXXXXX;Initial Catalog=XXXXXXXXX;Persist Security Info=True;User ID=XXXXXXXXX;Password=XXXXXXXXX" providerName="System.Data.SqlClient" />
			<commandText value="INSERT INTO Log ([Date],[Thread],[Level],[Logger],[Message],[Exception]) VALUES (@log_date, @thread, @log_level, @logger, @message, @exception)" />

			<parameter>
				<parameterName value="@log_date" />
				<dbType value="DateTime" />
				<layout type="log4net.Layout.RawTimeStampLayout" />
			</parameter>
			<parameter>
				<parameterName value="@thread" />
				<dbType value="String" />
				<size value="255" />
				<layout type="log4net.Layout.PatternLayout">
					<conversionPattern value="%thread" />
				</layout>
			</parameter>
			<parameter>
				<parameterName value="@log_level" />
				<dbType value="String" />
				<size value="50" />
				<layout type="log4net.Layout.PatternLayout">
					<conversionPattern value="%level" />
				</layout>
			</parameter>
			<parameter>
				<parameterName value="@logger" />
				<dbType value="String" />
				<size value="255" />
				<layout type="log4net.Layout.PatternLayout">
					<conversionPattern value="%logger" />
				</layout>
			</parameter>
			<parameter>
				<parameterName value="@message" />
				<dbType value="String" />
				<size value="4000" />
				<layout type="log4net.Layout.PatternLayout">
					<conversionPattern value="%message" />
				</layout>
			</parameter>
			<parameter>
				<parameterName value="@exception" />
				<dbType value="String" />
				<size value="2000" />
				<layout type="log4net.Layout.ExceptionLayout" />
			</parameter>
		</appender>

		<root>
			<level value="ALL" />
			<appender-ref ref="MainFileAppender" />
			<appender-ref ref="AdoNetAppender" />
		</root>
	</log4net>

	<!--Añadir appSettings-->
	<appSettings>
		<!--Ruta de los FTP, añadir el destino de los FTPs. eje: "D:\Origen\"-->
		<add key="ftpOriginRoute" value="sftp://ullr.XXXXX.es/XXXXXXXXXXXXX/XXXXXXXXXXXXX/Gyursel/Origen/"/>
		<add key="ftpDestinationRoute" value="sftp://ullr.XXXXXX.es/XXXXXXXXXXXXX/XXXXXXXXXXXXX/Gyursel/Destino/"/>
		<!--Usuario-->
		<add key="ftpUsername" value="XXXXXXXXXXXXX"/>
		<add key="ftpPassword" value="XXXXXXXXXXXXX"/>
		<!--Huella digital-->
		<add key="sshHostKeyFingerprint" value="ssh-rsa 0000 XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"/>
		<!--Puerto-->
		<add key="portOrigin" value="22"/>
		<add key="portDestination" value="22"/>
	</appSettings>
</configuration>
```
