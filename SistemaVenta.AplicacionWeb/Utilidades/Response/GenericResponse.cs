namespace SistemaVenta.AplicacionWeb.Utilidades.Response
{
    public class GenericResponse<TObject>
    {
        public bool Estado { get; set; }

        // la interrogacion permite que se pueda asignar valores nulos
        public string? Mensaje { get; set; } 
        public TObject? Objeto { get; set; } 
        public List<TObject>? ListaObjeto { get; set; }



    }
}
