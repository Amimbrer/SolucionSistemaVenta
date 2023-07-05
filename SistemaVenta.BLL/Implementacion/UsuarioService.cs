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
            // lanzar una excepción y detener el método si ya hay un usuario con el mismo correo
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

                // lógica para el envío de correo
                if(UrlPlantillaCorreo != null)
                {
                    UrlPlantillaCorreo = UrlPlantillaCorreo.Replace("[corre]", usuario_creado.Correo).Replace("[clave]", clave_generada);

                    string htmlCorreo = "";

                    // creamos una nueva solicitud para la url de la plantilla enviar clave
                    HttpWebRequest  request = (HttpWebRequest)WebRequest.Create(UrlPlantillaCorreo);

                    // obtenemos la respuesta de la solicitud de arriba
                    HttpWebResponse  response = (HttpWebResponse)request.GetResponse();

                    // validamos la solicitud
                    if(response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = response.GetResponseStream()) {
                            StreamReader? readerStream = null;

                            if(response.CharacterSet == null) { 
                            
                            readerStream = new StreamReader(dataStream);
                            
                            } else
                            // si contiene caracteres especiales se hace el dataStream pasándole el encoding
                            {
                                readerStream = new StreamReader(dataStream, Encoding.GetEncoding(response.CharacterSet));
                            }

                            htmlCorreo = readerStream.ReadToEnd();
                            response.Close();
                            readerStream.Close();
                        }
                    }

                    // cuando ya tenemos todo el html que vamos a enviar por correo
                    if(htmlCorreo != String.Empty)
                    {
                        await _correoService.EnviarCorreo(usuario_creado.Correo, "Cuenta Creada", htmlCorreo);

                    }

                    // volvemos a obtener el usuario creado para poder incluirle el rol
                    IQueryable<Usuario> query = await _repositorio.Consultar(u => u.IdUsuario == usuario_creado.IdUsuario);

                    // le incluimos el rol al usuarios
                    usuario_creado = query.Include(r => r.IdRolNavigation).First();

                }
                    return usuario_creado;
                            }
            catch (Exception ex)
            {

                throw;
            }
        }
        public async Task<Usuario> Editar(Usuario entidad, Stream Foto = null, string NombreFoto = "")
        {
            Usuario usuario_existe = await _repositorio.Obtener(U => U.Correo == entidad.Correo);

            if (usuario_existe != null)
                throw new TaskCanceledException("El correo ya existe");

            try {
                IQueryable<Usuario> queryUsuario = await _repositorio.Consultar(u => u.IdUsuario == entidad.IdUsuario);

                Usuario usuario_editar = queryUsuario.First();
                usuario_editar.Nombre = entidad.Nombre;
                usuario_editar.Correo= entidad.Correo;
                usuario_editar.Telefono = entidad.Telefono;
                usuario_editar.IdRol = entidad.IdRol;


                if (String.IsNullOrWhiteSpace(usuario_editar.NombreFoto)) {
                    usuario_editar.NombreFoto = entidad.NombreFoto;
                }

                if (Foto is not null) {
                    string urlFoto = await _fireBaseService.SubirStorage(Foto, "carpeta_usuario", usuario_editar.NombreFoto);
                    usuario_editar.UrlFoto = urlFoto;
                }

                bool respuesta = await _repositorio.Editar(usuario_editar);

                if (!respuesta) {
                    throw new TaskCanceledException("No se pudo modificar el usuario");
                }
                Usuario usuario_editado = queryUsuario.Include(r => r.IdRolNavigation).FirstOrDefault();

                return usuario_editado;

            } catch (Exception ex) {

                throw;
            }
        }

   
        public async Task<bool> Eliminar(int IdUsuario)
        {
            try {
                Usuario usuario_encontrado = await _repositorio.Obtener(u => u.IdUsuario == IdUsuario);

                if (usuario_encontrado is null) {
                    throw new TaskCanceledException("El usuario no existe");
                }

                string nombreFoto = usuario_encontrado.NombreFoto;
                bool respuesta = await _repositorio.Eliminar(usuario_encontrado);

                if (respuesta){
                    await _fireBaseService.EliminarStorage("carpeta_usuario", nombreFoto);
                }
                return respuesta;

            } catch (Exception) {

                throw ;
            }
        }


        public async Task<Usuario> ObtenerPorCredenciales(string correo, string clave) {
            string clave_encriptada = _utilidadesService.ConvertirSha256(clave);

            Usuario usuario_encontrado = await _repositorio.Obtener(u => u.Correo.Equals(correo) && u.Clave.Equals(clave_encriptada));

            return usuario_encontrado;
        }

        public async Task<Usuario> ObtenerPorId(int idUsuario) {
            IQueryable<Usuario> query = await _repositorio.Consultar(u => u.IdUsuario == idUsuario);

            Usuario resultado = query.Include(r => r.IdRolNavigation).FirstOrDefault();

            return resultado;
        }


        public async Task<bool> GuardarPerfil(Usuario entidad)
        {
            try {
                Usuario usuario_encontrado = await _repositorio.Obtener(u => u.IdUsuario == entidad.IdUsuario);

                if (usuario_encontrado is null) {
                    throw new TaskCanceledException("El usuario no existe");
                }

                usuario_encontrado.Correo = entidad.Correo;
                usuario_encontrado.Telefono = entidad.Telefono;
                
                bool respuesta = await _repositorio.Editar(usuario_encontrado);
                return respuesta;
            } catch (Exception) {
                throw;
            }
        }


        public async Task<bool> CambiarClave(int IdUsuario, string ClaveActual, string ClaveNueva) {
            try {
                Usuario usuario_encontrado = await _repositorio.Obtener(u => u.IdUsuario == IdUsuario);

                if (usuario_encontrado is null) {
                    throw new TaskCanceledException("El usuario no existe");
                }

                if (usuario_encontrado.Clave != _utilidadesService.ConvertirSha256(ClaveActual)) {
                    throw new TaskCanceledException("La contraseña ingresada como actual no es correcta");
                }

                usuario_encontrado.Clave = _utilidadesService.ConvertirSha256(ClaveNueva);

                bool respuesta = await _repositorio.Editar(usuario_encontrado);
                return respuesta;
            } catch (Exception) {
                throw;
            }
        
        
        }
        public async Task<bool> ReestablecerClave(string Correo, string UrlPlantillaCorreo = "")
        {
            try {
                Usuario usuario_encontrado = await _repositorio.Obtener(u =>u.Correo == Correo);

                if (usuario_encontrado is null) {
                    throw new TaskCanceledException("No se ha encontrado ningún usuario asociado al correo");
                }

                string clave_generada = _utilidadesService.GenerarClave();
                usuario_encontrado.Clave = _utilidadesService.ConvertirSha256(clave_generada);

                UrlPlantillaCorreo = UrlPlantillaCorreo.Replace("[clave]", clave_generada);

                string htmlCorreo = "";

                // creamos una nueva solicitud para la url de la plantilla enviar clave
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UrlPlantillaCorreo);

                // obtenemos la respuesta de la solicitud de arriba
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // validamos la solicitud
                if (response.StatusCode == HttpStatusCode.OK) {
                    using (Stream dataStream = response.GetResponseStream()) {
                        StreamReader? readerStream = null;

                        if (response.CharacterSet == null) {

                            readerStream = new StreamReader(dataStream);

                        } else
                        // si contiene caracteres especiales se hace el dataStream pasándole el encoding
                        {
                            readerStream = new StreamReader(dataStream, Encoding.GetEncoding(response.CharacterSet));
                        }

                        htmlCorreo = readerStream.ReadToEnd();
                        response.Close();
                        readerStream.Close();
                    }
                }

                    bool correo_enviado = false;


                    // cuando ya tenemos todo el html que vamos a enviar por correo
                    if (htmlCorreo != String.Empty) {
                        correo_enviado =  await _correoService.EnviarCorreo(Correo, "Contraseña reestablecida", htmlCorreo);
                    }

                    if (!correo_enviado) {
                        throw new TaskCanceledException("Tenemos probelemas por favor intentelo mas tarde");
                    }

                    bool respuesta = await _repositorio.Editar(usuario_encontrado);
                    return respuesta;

            } catch (Exception) {

                throw;
            }
        }
    }
}
