using Domain;
using System.Data;

namespace Repository
{
    public interface IDapper:IDisposable
    {
        IDbConnection GetConnection(string connectionString);
        void SetConnectionString(string connection);
        void SetConnectionStringHttpGateway(string connection);

        Task<IEnumerable<Configuracion_Cliente>> GetConfiguracionesClientesActivos();
        Task<IEnumerable<Configuracion_Cliente>> GetAllConfiguracionesClientes();

        Task InsertarError(Configuracion_Cliente datos, string nombreBase, string mensajeError, string connectionString, int tamanoBase);

        Task<Templates> GetTemplate(int id);
        Task<Errores_Procesamiento> GetError();

        Task ActualizarMessageEmail(int id, string response);
        Task<int> InsertarMessageEmail(Templates template, string request);


    }
}
