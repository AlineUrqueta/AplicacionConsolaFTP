﻿using Domain;
using FluentFTP;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Repository;
using System.Net.Http.Headers;
using System.Text;
using File = System.IO.File;

namespace AplicacionConsolaSMS
{
    internal class Program
    {
        static readonly HttpClient client = new HttpClient();
        static async Task Main(string[] args)
        {
            string appSettingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            IConfigurationRoot configuration = null;
            configuration = new ConfigurationBuilder().
                SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true)
                .Build();


            //Conexion BD
            var connectionString = configuration.GetConnectionString("PruebaConfiguracion");
            var connectionStringHttpGateway = configuration.GetConnectionString("HttpGateway");

            var IDapper = new Dapperr();
            IDapper.SetConnectionString(connectionString);
            IDapper.SetConnectionStringHttpGateway(connectionStringHttpGateway);

            //Templates
            Templates dataTemplatesProcesado = await IDapper.GetTemplate(6);
            Templates dataTemplatesError = await IDapper.GetTemplate(7);



            //Config SFTP
            ISFTP sftp = new SFTP();
            SftpClient connectionSftp = null;
            Credenciales_SFTP_Interno credenciales = new Credenciales_SFTP_Interno();
            configuration.GetSection("SftpInterno").Bind(credenciales);

            //Config FTP
            IFTP ftp = new FTP();
            FtpClient connectionFtp = null;
            Credenciales_FTP_Interno credencialesFtp = new Credenciales_FTP_Interno();
            configuration.GetSection("FTPInterno").Bind(credencialesFtp);


            //Carpeta temporal
            string temp = configuration["CarpetaTemporal"];
            string rutaCarpetaTemp = await CrearCarpetaTemporal(temp);



            try
            {
                var data = await IDapper.GetConfiguracionesClientesActivos();


                foreach (Configuracion_Cliente item in data)
                {
                    if (string.IsNullOrEmpty(item.Server) || string.IsNullOrEmpty(item.Pass) || string.IsNullOrEmpty(item.User) || item.Puerto == null || item.Puerto < 0)
                    {
                        await Console.Out.WriteLineAsync($"Configuracion faltante en datos FTP/SFTP para el cliente {item.Cliente}\n");
                    }


                    if (item.EsSFTP == true)
                    {
                        connectionSftp = await sftp.Conectar(item);
                        await Console.Out.WriteLineAsync("Cliente : " + item.Cliente);
                        if (connectionSftp.IsConnected)
                        {
                            List<SftpFile> archivos = await sftp.ObtenerArchivos(item.RutaOrigen, connectionSftp);
                            await sftp.DescargarArchivos(rutaCarpetaTemp, archivos, connectionSftp);
                            await Console.Out.WriteLineAsync();

                            string[] archivosTemporales = Directory.GetFiles(rutaCarpetaTemp);

                            foreach (string archivo in archivosTemporales)
                            {
                                
                                try
                                {
                                    SftpClient connectionSftpInterno = null;
                                    connectionSftpInterno = await sftp.ConectarSftpInterno(credenciales);

                                    string nomArchivo = Path.GetFileName(archivo);
                                    SftpFile archivoProcesar = await sftp.ObtenerArchivo(item.RutaOrigen, nomArchivo, connectionSftp);

                                    int tamanoBase = await ObtenerTamanoBase(archivo, item);

                                    if (VerificarArchivoCSVMod(archivo, item, tamanoBase) == true)
                                    {


                                        if (archivoProcesar != null)
                                        {
                                            string rutaOrigenErrores = item.RutaOrigen + "Errores";
                                            await sftp.MoverArchivo(rutaOrigenErrores, archivoProcesar, connectionSftp);

                                            Errores_Procesamiento dataError = await IDapper.GetError();

                                            string templateError = ReemplazarDatosTemplate(dataError, item, dataTemplatesError);
                                            string request = CrearJson(dataTemplatesError, templateError);
                                            string response = await RespuestaApi(request);
                                            int id = await IDapper.InsertarMessageEmail(dataTemplatesError, request);
                                            await IDapper.ActualizarMessageEmail(id, response);

                                        }

                                    }
                                    else
                                    {

                                        if (archivoProcesar != null)
                                        {
                                            string mensaje = "";
                                            if (connectionSftpInterno.IsConnected)
                                            {

                                                string rutaBase = item.RutaDestino + credenciales.RutaBases;
                                                string rutaSolicitud = item.RutaDestino + credenciales.RutaSolicitudes;

                                                string nombreArchivoBase = await ProcesarArchivoBase(archivo, rutaBase, connectionSftpInterno);
                                                string rutaArchivoBase = rutaBase + nombreArchivoBase;

                                                SftpFile archivoBase = await sftp.ObtenerArchivo(rutaBase, nombreArchivoBase, connectionSftpInterno);

                                                await sftp.DescargarArchivo(rutaCarpetaTemp, archivoBase, connectionSftpInterno);
                                                string rutaArchivoBaseTemporal = Path.Combine(rutaCarpetaTemp, archivoBase.Name);

                                                mensaje = await ProcesarArchivoSolicitud(rutaArchivoBaseTemporal, rutaSolicitud, connectionSftpInterno, item, rutaCarpetaTemp, nomArchivo);

                                                await sftp.Desconectar(connectionSftpInterno);

                                            }
                                            string rutaProcesados = item.RutaOrigen + "Procesados";
                                            await sftp.MoverArchivo(rutaProcesados, archivoProcesar, connectionSftp);


                                            string templateProcesado = ReemplazarDatosTemplateProcesados(archivoProcesar.Name, item, dataTemplatesProcesado, tamanoBase, mensaje);
                                            string requestProcesado = CrearJson(dataTemplatesProcesado, templateProcesado);
                                            string responseProcesado = await RespuestaApi(requestProcesado);
                                            int id = await IDapper.InsertarMessageEmail(dataTemplatesError, requestProcesado);
                                            await IDapper.ActualizarMessageEmail(id, responseProcesado);


                                        }

                                    }
                                }
                                catch (Exception ex)
                                {
                                    await Console.Out.WriteLineAsync(ex.Message);
                                }


                            }

                            await EliminarArchivosCarpetaTemporal(rutaCarpetaTemp);

                        }
                        await sftp.Desconectar(connectionSftp);

                    }
                    else if (item.EsSFTP == false)
                    {
                        connectionFtp = ftp.ConnectarFTP(item.Server, item.Pass, item.User, item.Puerto);
                        if (connectionFtp.IsConnected)
                        {
                            //Descargar archivos del FTP
                            List<string> rutaArchivos = await RutaArchivosTempFTP(rutaCarpetaTemp, connectionFtp, item.RutaOrigen);
                            foreach (string archivo in rutaArchivos)
                            {
      
                                try
                                {
                                    FtpClient connectionFtpInterno= null;
                                    connectionFtpInterno = ftp.ConnectarFTPInterno(credencialesFtp);
                                    if (connectionFtpInterno.IsConnected)
                                    {
                                        string nomArchivo = Path.GetFileName(archivo);
                                        int tamanoBase = await ObtenerTamanoBase(archivo, item);

                                        string rutaArchivoOrigen = item.RutaOrigen + "/" + nomArchivo;
                                        string rutaArchivoDestinoError = item.RutaOrigen + "/Errores/" + nomArchivo;
                                        string rutaArchivoDestinoProcesados = item.RutaOrigen + "/Procesados/" + nomArchivo;


                                        if (VerificarArchivoCSVMod(archivo, item, tamanoBase) == true) // Posee errores
                                        {

                                            await ftp.MoverArchivosCarpeta(rutaArchivoOrigen, rutaArchivoDestinoError, connectionFtp);
                                            Errores_Procesamiento dataError = await IDapper.GetError();
                                            string templateError = ReemplazarDatosTemplate(dataError, item, dataTemplatesError);
                                            string request = CrearJson(dataTemplatesError, templateError);
                                            string response = await RespuestaApi(request);
                                            int id = await IDapper.InsertarMessageEmail(dataTemplatesError, request);
                                            await IDapper.ActualizarMessageEmail(id, response);

                                        }
                                        else
                                        {

                                            string mensaje = ""; //Mensaje SMS Archivo de solicitud

                                            string rutaBaseFTP = item.RutaDestino + credenciales.RutaBases;
                                            string rutaSolicitudFTP = item.RutaDestino + credenciales.RutaSolicitudes;

                                            string nombreArchivoBase = await ProcesarArchivoBaseFTP(archivo,rutaBaseFTP, connectionFtpInterno);

                                            mensaje = await ProcesarArchivoSolicitudFTP( archivo, rutaSolicitudFTP, connectionFtpInterno, item, rutaCarpetaTemp);

                                            await ftp.MoverArchivosCarpeta(rutaArchivoOrigen, rutaArchivoDestinoProcesados, connectionFtp);

                                            string templateProcesado = ReemplazarDatosTemplateProcesados(nomArchivo, item, dataTemplatesProcesado, tamanoBase, mensaje);
                                            string requestProcesado = CrearJson(dataTemplatesProcesado, templateProcesado);
                                            string responseProcesado = await RespuestaApi(requestProcesado);
                                            int id = await IDapper.InsertarMessageEmail(dataTemplatesError, requestProcesado);
                                            await IDapper.ActualizarMessageEmail(id, responseProcesado);

                                        }
                                    }

                                }
                                catch (Exception ex)
                                {
                                    await Console.Out.WriteLineAsync(ex.Message);
                                }

                            }


                            await EliminarArchivosCarpetaTemporal(rutaCarpetaTemp);
                        }
                        
                    }
                    else
                    {
                        Console.WriteLine("No se ha podido identificar el tipo de servidor: SFTP/FTP\n");
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }


        }

        private static string ReemplazarDatosTemplateProcesados(string nombreArchivo,Configuracion_Cliente item,Templates dataTemplate, int tamanoBase,string mensaje)
        {
            string template = dataTemplate.Template;
            DateTime fecha = DateTime.Now;
            TimeSpan horaActual = new TimeSpan(fecha.Hour, fecha.Minute, fecha.Second);


            if (item.InicioAutomatico == false)
            {
                if (item.HoraInicio < horaActual)
                {
                    fecha = fecha.AddDays(1);
                    horaActual = item.HoraInicio;
                }
            }

            template = template.Replace("[[Usuario]]", item.Cliente);
            template = template.Replace("[[Rol]]", item.RolID);
            template = template.Replace("[[Nombre]]", nombreArchivo);
            template = template.Replace("[[Tamanio]]", tamanoBase.ToString());
            template = template.Replace("[[Fecha]]", fecha.ToString("dd-MM-yyyy"));
            template = template.Replace("[[Hora]]", horaActual.ToString(@"hh\:mm"));
            template = template.Replace("[[Mensaje]]", mensaje);
            return template;
        }

        private static string ReemplazarDatosTemplate(Errores_Procesamiento dataError, Configuracion_Cliente item, Templates dataTemplate)
        {
            string template = dataTemplate.Template;
            TimeSpan horaInicio = item.InicioAutomatico ? DateTime.Now.TimeOfDay : item.HoraInicio;

            template = template.Replace("[[Usuario]]", item.Cliente);
            template = template.Replace("[[Rol]]", item.RolID);
            template = template.Replace("[[Nombre]]", dataError.NombreBase);
            template = template.Replace("[[Tamanio]]", dataError.TamanoBase.ToString());
            template = template.Replace("[[Error]]", dataError.Error);
            template = template.Replace("[[Fecha]]", dataError.FechaError.ToString("dd-MM-yyyy"));
            template = template.Replace("[[Hora]]", horaInicio.ToString(@"hh\:mm"));

            return template;
        }

        private static async Task<int> ObtenerTamanoBase(string rutaArchivo, Configuracion_Cliente datos) // Cabecera 
        {
            int tamanoBase = 0;
            using (var reader = new StreamReader(rutaArchivo))
            {
                string linea;
                while ((linea = await reader.ReadLineAsync()) != null)
                {
                    tamanoBase++;
                }
            }
            tamanoBase = datos.TieneCabecera ? tamanoBase - 1 : tamanoBase; //Tamaño de la base cuenta la cabecera o solo los envíos? 
            return tamanoBase;
        }

        private static async Task<string> CrearCarpetaTemporal(string carpetaTemporal)
        {
            try
            {
                var projpath = new Uri(Path.Combine(new string[] { System.AppDomain.CurrentDomain.BaseDirectory, "..\\..\\.." })).AbsolutePath;
                if (!Directory.Exists(Path.Combine(projpath, carpetaTemporal)))
                {
                    Directory.CreateDirectory(Path.Combine(projpath, carpetaTemporal));
                }
                return await Task.Run(() => Path.Combine(projpath, carpetaTemporal));
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static async Task EliminarArchivosCarpetaTemporal(string path)
        {

            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    DirectoryInfo di = new DirectoryInfo(path);

                    await Task.Run(() =>
                    {
                        foreach (FileInfo file in di.GetFiles())
                        {
                            file.Delete();

                        }
                    });
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static async Task<string> ProcesarArchivoBase(string rutaArchivoLocal, string rutaDestino, SftpClient connectionSftp)
        {
            ISFTP sftp = new SFTP();

            DateTime fechaActual = DateTime.Now;
            string fechaFormateada = fechaActual.ToString("yyyyMMddHHmmss");

            string nombreBase = Path.GetFileNameWithoutExtension(rutaArchivoLocal);
            string nombreArchivoFinal = $"{nombreBase}_{fechaFormateada}_tmp.csv";

            if (!connectionSftp.Exists(rutaDestino))
            {
                Console.WriteLine($"La ruta de destino {rutaDestino} no existe en el servidor SFTP.");
                
            }

            try
            {
                using (FileStream fileStream = new FileStream(rutaArchivoLocal, FileMode.Open, FileAccess.Read))
                {
                    var respuesta = sftp.SubirArchivo(connectionSftp, fileStream, rutaDestino, nombreArchivoFinal);
                    await Console.Out.WriteLineAsync(respuesta);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
            }
            return nombreArchivoFinal;
        }

        public static async Task<string> ProcesarArchivoSolicitud(string rutaArchivoBase, string rutaDestino, SftpClient connectionSftp, Configuracion_Cliente datos,string rutaTemporal,string archivo)
        {
            ISFTP sftp = new SFTP();

            try
            {
                var registros = new List<string>();
                string mensaje;
                int envios = 0;

                using (var reader = new StreamReader(rutaArchivoBase))
                {
                    string linea;
                    while ((linea = await reader.ReadLineAsync()) != null)
                    {
                        registros.Add(linea);
                        envios++;
                    }
                }
                int indiceComa = registros[1].IndexOf(',');
                
                if (datos.TieneCabecera)
                {

                    mensaje = registros[1].Substring(indiceComa+1);
                    envios = registros.Count - 1;
                }
                else
                {
                    mensaje = registros[0].Substring(indiceComa + 1); ;
                    envios = registros.Count;
                }


                DateTime fecha = DateTime.Now;
                TimeSpan horaActual = new TimeSpan(fecha.Hour, fecha.Minute, fecha.Second);

                if (datos.InicioAutomatico == false)
                {
                    if(datos.HoraInicio < horaActual)
                    {
                        fecha = fecha.AddDays(1);
                        horaActual = datos.HoraInicio;
                    }
                }

                string horaInicioFormateada = horaActual.ToString(@"HH\:mm");

                DateTime fechaActualArchivo = DateTime.Now;


                string fechaFormateada = fechaActualArchivo.ToString("yyyyMMdd");
                string horaFormateada = fechaActualArchivo.ToString("HHmmss");


                string nombreArchivoFinal = $"solicitud_{datos.Cliente}_{fechaFormateada}_{horaFormateada}.csv";
                string nombreBase = Path.GetFileNameWithoutExtension(rutaArchivoBase);
                string rutaArchivoFinal = Path.Combine(rutaTemporal, nombreArchivoFinal);
                

                using (var writer = new StreamWriter(rutaArchivoFinal))
                {
                    writer.WriteLine("id_cliente;id_producto;nombre;num_origen;mensaje;envios;fecha;hora;base;CentroCosto");
                    string[] campos = new string[]
                    {
                        datos.IdCliente.ToString(),
                        datos.IdProducto,
                        Path.GetFileNameWithoutExtension(archivo),
                        "0",
                        mensaje,
                        envios.ToString(),
                        fecha.ToString("dd-MM-yyyy"),
                        horaInicioFormateada,
                        Path.GetFileName(rutaArchivoBase),
                        "0"
                            };
                    await writer.WriteLineAsync(string.Join(";", campos));
                }

                using (FileStream fileStream = new FileStream(rutaArchivoFinal, FileMode.Open, FileAccess.Read))
                {
                    var respuesta = sftp.SubirArchivo(connectionSftp, fileStream, rutaDestino, nombreArchivoFinal);
                    await Console.Out.WriteLineAsync(respuesta);
                }

                mensaje = mensaje.Trim('"');
                return mensaje;
               

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
                return "";
            }
            
        }

        public static bool VerificarArchivoCSVMod(string rutaArchivo, Configuracion_Cliente item, int tamanoBase)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var IDapper = new Dapperr();
            var connectionString = configuration.GetConnectionString("PruebaConfiguracion");

            if (item.TieneCabecera == false)
            {
                IDapper.InsertarError(item, rutaArchivo, "La base no posee cabecera", connectionString, tamanoBase);
                return true;
            }

            if (Path.GetExtension(rutaArchivo) == ".csv")
            {
                string[] lineas = File.ReadAllLines(rutaArchivo);

                if (lineas.Length > 0)
                {
                    string formatoCabecera = lineas[0];
                    if (formatoCabecera != "PHONE_NUMBER,MESSAGE")
                    {
                        IDapper.InsertarError(item, rutaArchivo, "El formato de la cabecera no es correcto", connectionString, tamanoBase);
                        return true;
                    }

                    for (int j = 1; j < lineas.Length; j++)
                    {
                        string linea = lineas[j];
                        int indicePrimeraComa = linea.IndexOf(',');

                        if (indicePrimeraComa == -1)
                        {
                            IDapper.InsertarError(item, rutaArchivo, "No se ha encontrado el separador coma" , connectionString, tamanoBase);
                            return true;
                        }
                        else
                        {
                            string numero = linea.Substring(0, indicePrimeraComa);

                            if (linea.Contains(';'))
                            {
                                IDapper.InsertarError(item, rutaArchivo, "El campo presenta caracteres especiales", connectionString, tamanoBase);
                                return true;
                            }
                            else
                            {
                                if (numero.Contains(" "))
                                {
                                    IDapper.InsertarError(item, rutaArchivo, "El numero presenta espacios", connectionString, tamanoBase);
                                    return true;
                                }

                                if (!long.TryParse(numero, out _))
                                {
                                    IDapper.InsertarError(item, rutaArchivo, "Numero de celular incorrecto", connectionString, tamanoBase);
                                    return true;
                                }


                            }
                        }

                    }
                }
                
            }
            else
            {
                IDapper.InsertarError(item, rutaArchivo, "La extensión del archivo no es .csv", connectionString, tamanoBase);
                return true;
            }

            return false;
        }

        public static string CrearJson(Templates dataTemplate, string template)
        {
            List<string> correos = new List<string>(dataTemplate.CorreosDestino.Split(','));
            List<Recipient> recipients = correos.Select(correo => new Recipient { To = correo }).ToList();

            var Datafinal = new FormatoJson
            {
                Subject = "Alerta de campaña automática SMS",
                From = dataTemplate.CorreoSalida,
                Template = new TemplateJson
                {
                    Type = "text/html",
                    Value = template
                },
                Recipients = recipients
            };

            var request = JsonConvert.SerializeObject(Datafinal);
            return request;
        }

        public static async Task<string> RespuestaApi(string request)
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

            string username = configuration.GetSection("ApiMasiv")["user"];
            string password = configuration.GetSection("ApiMasiv")["pass"];

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));

            try
            {
                string url = configuration.GetSection("ApiMasiv")["url"];

                var contenido = new StringContent(request, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, contenido);

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("Excepcion :{0} ", e.Message);
                return "";
            }
        }


        //FTP

        public static async Task<List<string>> RutaArchivosTempFTP(string rutaCarpetaTemp,FtpClient ftpClient,string rutaOrigen)
        {

             List<string> rutasDescargadas = new List<string>(); 

            try
            {
                ftpClient.Connect(); 

                var archivos = ftpClient.GetListing(rutaOrigen);

                foreach (var archivo in archivos)
                {
                    if (archivo.Type == FtpObjectType.File && Path.GetExtension(archivo.FullName) == ".csv")
                    {
                        string remoto = Path.Combine(rutaOrigen, archivo.Name);
                        string local = Path.Combine(rutaCarpetaTemp, archivo.Name);
                        ftpClient.DownloadFile(local, remoto);
                        Console.WriteLine($"Archivo descargado: {local}");

                        rutasDescargadas.Add(local);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al descargar archivos: {ex.Message}");
            }
            
            return rutasDescargadas;
        }

        public static async Task<string> ProcesarArchivoBaseFTP(string archivo, string rutaBaseFTP, FtpClient client)
        {

            DateTime fechaActual = DateTime.Now;
            string fechaFormateada = fechaActual.ToString("yyyyMMddHHmmss");

            string nombreBase = Path.GetFileNameWithoutExtension(archivo);
            string nombreArchivoFinal = $"{nombreBase}_{fechaFormateada}_tmp.csv";

            try
            {
                bool directoryExists = await Task.Run(() => client.DirectoryExists(rutaBaseFTP));

                if (!directoryExists)
                {
                    Console.WriteLine($"La ruta de destino {rutaBaseFTP} no existe en el servidor FTP.");
                }
                else
                {
                    await Task.Run(() => client.UploadFile(archivo, $"{rutaBaseFTP}/{nombreArchivoFinal}"));
                    Console.WriteLine($"Archivo {archivo} subido exitosamente a {rutaBaseFTP} en el servidor FTP.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
            }

            return nombreArchivoFinal;
        }

        public static async Task<string> ProcesarArchivoSolicitudFTP(string rutaArchivoBase, string rutaDestino, FtpClient client, Configuracion_Cliente datos, string rutaTemporal)
        {
            try
            {
                var registros = new List<string>();
                string mensaje;
                int envios = 0;

                using (var reader = new StreamReader(rutaArchivoBase))
                {
                    string linea;
                    while ((linea = await reader.ReadLineAsync()) != null)
                    {
                        registros.Add(linea);
                        envios++;
                    }
                }
                int indiceComa = registros[1].IndexOf(',');

                if (datos.TieneCabecera)
                {
                    mensaje = registros[1].Substring(indiceComa + 1);
                    envios = registros.Count - 1;
                }
                else
                {
                    mensaje = registros[0].Substring(indiceComa + 1); ;
                    envios = registros.Count;
                }

                DateTime fecha = DateTime.Now;
                TimeSpan horaActual = new TimeSpan(fecha.Hour, fecha.Minute, fecha.Second);

                if (!datos.InicioAutomatico && datos.HoraInicio < horaActual)
                {
                    fecha = fecha.AddDays(1);
                    horaActual = datos.HoraInicio;
                }

                string horaInicioFormateada = horaActual.ToString(@"hh\:mm");

                DateTime fechaActualArchivo = DateTime.Now;

                string fechaFormateada = fechaActualArchivo.ToString("yyyyMMdd");
                string horaFormateada = fechaActualArchivo.ToString("HHmmss");

                string nombreArchivoFinal = $"solicitud_{datos.Cliente}_{fechaFormateada}_{horaFormateada}.csv";
                string rutaArchivoFinal = Path.Combine(rutaTemporal, nombreArchivoFinal);

                using (var writer = new StreamWriter(rutaArchivoFinal))
                {
                    writer.WriteLine("id_cliente;id_producto;nombre;num_origen;mensaje;envios;fecha;hora;base;CentroCosto");
                    string[] campos = new string[]
                    {
                    datos.IdCliente.ToString(),
                    datos.IdProducto,
                    Path.GetFileNameWithoutExtension(rutaArchivoBase),
                    "0",
                    mensaje,
                    envios.ToString(),
                    fecha.ToString("dd-MM-yyyy"),
                    horaInicioFormateada,
                    Path.GetFileName(rutaArchivoBase),
                    "0"
                        };
                    await writer.WriteLineAsync(string.Join(";", campos));
                }

                await Task.Run(() => client.UploadFile(rutaArchivoFinal, $"{rutaDestino}/{nombreArchivoFinal}"));
                Console.WriteLine($"Archivo {nombreArchivoFinal} subido exitosamente a {rutaDestino} en el servidor FTP.");

                mensaje = mensaje.Trim('"');
                return mensaje;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
                return "";
            }
        }


    }

}










