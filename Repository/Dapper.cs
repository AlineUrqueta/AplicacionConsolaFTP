using Dapper;
using Domain;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;


namespace Repository
{
    public class Dapperr : IDapper
    {
        private string connectionString = string.Empty;
        private string connectionStringHttpGateway = string.Empty;

        public void Dispose()
        {
        }


        public IDbConnection GetConnection(string connectionString)
        {
            try
            {
                IDbConnection connection = new SqlConnection(connectionString);
                return connection;
                
            }
            catch (SqlException ex)
            {
                Console.WriteLine("Error de conexión: " + ex.Message);
                throw;
            }


        }

        public async Task<IEnumerable<Configuracion_Cliente>> GetAllConfiguracionesClientes()
        {
            IEnumerable<Configuracion_Cliente> data = null;

            try
            {
                using (var db = new SqlConnection(connectionString))
                {
                    db.Open();
                    data = await db.QueryAsync<Configuracion_Cliente>("dbo.GetAllConfiguracionClientes");
                    db.Close();
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync(e.Message);
            }

            return data;
        }

        public async Task<IEnumerable<Configuracion_Cliente>> GetConfiguracionesClientesActivos()
        {
            IEnumerable<Configuracion_Cliente> data = null;

            try
            {
                using (var db = new SqlConnection(connectionString))
                {
                    db.Open();
                    data = await db.QueryAsync<Configuracion_Cliente>("dbo.GetConfiguracionClientesActivos");
                    db.Close();
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync(e.Message);
            }

            return data;
        }

        public void SetConnectionString(string connection)
        {
            this.connectionString = connection;
        }

        public void SetConnectionStringHttpGateway(string connection)
        {
            this.connectionStringHttpGateway = connection;
        }

        public async Task InsertarError(Configuracion_Cliente datos, string nombreBase, string mensajeError, string connectionString, int tamanoBase)
        {

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                DateTime fechaActual = DateTime.Now;
                var parametros = new DynamicParameters();
                parametros.Add("@IdCliente", datos.IdCliente);
                parametros.Add("@NombreBase", Path.GetFileName(nombreBase));
                parametros.Add("@Error", mensajeError);
                parametros.Add("@FechaError", fechaActual);
                parametros.Add("@TamanoBase", tamanoBase);

                connection.Execute("dbo.InsertarErroresProcesamiento", parametros, commandType: CommandType.StoredProcedure);

                Console.WriteLine("Datos insertados correctamente en tabla Errores.");
                connection.Close();
            }


        }

        public async Task<Templates> GetTemplate(int id)
        {
            Templates template = null;

            try
            {
                using (var db = new SqlConnection(connectionStringHttpGateway))
                {
                    db.Open();
                    var templates = await db.QueryAsync<Templates>("SP_GetTemplateEmail_Sendia", new { id }, commandType: CommandType.StoredProcedure);
                    template = templates.FirstOrDefault();
                    db.Close();
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync(e.Message);
            }

            return template;
        }

        public async Task<Errores_Procesamiento> GetError()
        {
            Errores_Procesamiento error = null;

            try
            {
                using (var db = new SqlConnection(connectionString))
                {
                    db.Open();
                    var data = await db.QueryAsync<Errores_Procesamiento>("dbo.GetUltimoError", commandType: CommandType.StoredProcedure);
                    error = data.FirstOrDefault();
                    db.Close();
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync(e.Message);
            }

            return error;
        }

        public async Task<int> InsertarMessageEmail(Templates template, string request)
        {
            using (var connection = new SqlConnection(connectionStringHttpGateway))
            {
                connection.Open();

                var parametros = new DynamicParameters();
                parametros.Add("@Proceso", 1);
                parametros.Add("@Id", "");
                parametros.Add("@Subject", "Alerta procesamiento de bases");
                parametros.Add("@FromTo", template.CorreoSalida);
                parametros.Add("@Recipients", template.CorreosDestino);
                parametros.Add("@TemplateId", template.Id);
                parametros.Add("@DeliveryId", "");
                parametros.Add("@Request", request);
                parametros.Add("@Response", "");
                parametros.Add("@IdUserEnvio", 999); //Id de Usuario que se configuró como usuario 
 
                int id = await connection.ExecuteScalarAsync<int>("dbo.SP_MessageEmail", parametros, commandType: CommandType.StoredProcedure);

                Console.WriteLine("Datos insertados correctamente en tabla MessageEmail.");
                connection.Close();
                return id;
            }
        }

        public async Task ActualizarMessageEmail( int id,string response)
        {
            
            JObject objeto = JsonConvert.DeserializeObject<JObject>(response);
            string deliveryId = (string)objeto["data"]["deliveryId"];

            using (var connection = new SqlConnection(connectionStringHttpGateway))
            {
                connection.Open();

                var parametros = new DynamicParameters();
                parametros.Add("@Proceso", 2);
                parametros.Add("@Id", id.ToString());
                parametros.Add("@Subject", "");
                parametros.Add("@FromTo", "");
                parametros.Add("@Recipients", "");
                parametros.Add("@TemplateId", "");
                parametros.Add("@DeliveryId", deliveryId);
                parametros.Add("@Request", "");
                parametros.Add("@Response", response);
                parametros.Add("@IdUserEnvio", ""); //Id de Usuario que se configuró como usuario 

                connection.Execute("dbo.SP_MessageEmail", parametros, commandType: CommandType.StoredProcedure);

                Console.WriteLine("Datos actualizados correctamente en tabla MessageEmail.");
                connection.Close();

            }
        }


    }
}
