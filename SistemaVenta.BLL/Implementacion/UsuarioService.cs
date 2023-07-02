using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using SistemaVenta.BLL.Interfaces;
using SistemaVenta.DAL.Interfaces;
using SistemaVenta.Entity;

namespace SistemaVenta.BLL.Implementacion
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IGenericRepository<Usuario> _repositorio;
        private readonly IFirebaseService _fireBaseService;
        private readonly IUtilidadesService _utilidadesService;
        private readonly ICorreoService _correoService;

        public UsuarioService (IGenericRepository<Usuario> repositorio, IFirebaseService fireBaseService, IUtilidadesService utilidadesService, ICorreoService correoService)
        {
            _repositorio = repositorio;
            _fireBaseService = fireBaseService;
            _utilidadesService = utilidadesService;
            _correoService = correoService;
        }

        public async Task<List<Usuario>> Lista()
        {
          IQueryable<Usuario> query = await _repositorio.Consultar();
            return query.Include(rol => rol.IdRolNavigation).ToList();
        }

        public async Task<Usuario> Crear(Usuario entidad, Stream Foto = null, string NombreFoto = "", string UrlPlantillaCorreo = "")
        {
            // validar si existe el usuario
            Usuario usuario_existe = await _repositorio.Obtener(u => u.Correo == entidad.Correo);

            if(usuario_existe != null)
            // lanzar una excepcion y detener el metodo si ya hay un usuario con el mismo correo
                throw new TaskCanceledException("El correo ya existe");


            try
            {
                string clave_generada = _utilidadesService.GenerarClave();
                entidad.Clave = _utilidadesService.ConvertirSha256(clave_generada);
                entidad.NombreFoto = NombreFoto; 

                if (Foto != null)
                {
                    string urlFoto = await _fireBaseService.SubirStorage(Foto, "carpeta_usuario", NombreFoto);
                    entidad.UrlFoto = urlFoto;
                }

                Usuario usuario_creado = await _repositorio.Crear(entidad);

                if(usuario_creado.IdUsuario == 0)
                    throw new TaskCanceledException("No se pudo crear el usuario");

                // logica para el envio de correo
                if(UrlPlantillaCorreo != null)
                {
                    UrlPlantillaCorreo = UrlPlantillaCorreo.Replace("[corre]", usuario_creado.Correo).Replace("[clave]", clave_generada);

                    string htmlCorreo = "";

                    // creamo una nueva solicitud para la url de la plantilla enviar clave
                    HttpWebRequest  request = (HttpWebRequest)WebRequest.Create(UrlPlantillaCorreo);

                    // obtenemos la respuesta de la solicitud de arriba
                    HttpWebResponse  response = (HttpWebResponse)request.GetResponse();

                    // validamos la solicitud
                    if(response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = response.GetResponseStream()) {
                            StreamReader readerStream = null;

                            if(response.CharacterSet == null) { 
                            
                            readerStream = new StreamReader(dataStream);
                            
                            } else
                            // si contiene caracteres especiales se hace el dataStream pasandole el encoding
                            {
                                readerStream = new StreamReader(dataStream, Encoding.GetEncoding(response.CharacterSet));
                            }

                            htmlCorreo = readerStream.ReadToEnd();
                            response.Close();
                            readerStream.Close();
                        }

                        // cuando ya tenemos todo el html que vamos a enviar por correo
                        if(htmlCorreo != String.Empty)
                        {
                            await _correoService.EnviarCorreo(usuario_creado.Correo, "Cuenta Creada", htmlCorreo);

                        }
                    }

                    // volvemos a obtener el usuario creado para poder incluirle el rol
                    IQueryable<Usuario> query = await _repositorio.Consultar(u => u.IdUsuario == usuario_creado.IdUsuario);

                    // le incluimos el rol al usuarios
                    usuario_creado = query.Include(r => r.IdRolNavigation).First();

                    return usuario_creado;
                }
                            }
            catch (Exception ex)
            {

                throw;
            }
        }
        public Task<Usuario> Editar(Usuario entidad, Stream Foto = null, string NombreFoto = "")
        {
            throw new NotImplementedException();
        }

        public Task<bool> CambiarClave(int IdUsuario, string ClaveActual, string ClaveNueva)
        {
            throw new NotImplementedException();
        }


        public Task<bool> Eliminar(int IdUsuario)
        {
            throw new NotImplementedException();
        }

        public Task<bool> GuardarPerfil(Usuario entidad)
        {
            throw new NotImplementedException();
        }

       

        public Task<Usuario> ObtenerPorCredenciales(string correo, string clave)
        {
            throw new NotImplementedException();
        }

        public Task<Usuario> ObtenerPorId(int idUsuario)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReestablecerClave(string Correo, string UrlPlantillaCorreo = "")
        {
            throw new NotImplementedException();
        }
    }
}
