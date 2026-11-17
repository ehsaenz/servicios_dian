using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using DIAN.Impresion;

namespace DIAN.Pos.Api
{
    /// <summary>
    /// Carga el generador de PDF (DevExpress + Skia) desde su propia carpeta y en su propio contexto de carga: así sus
    /// versiones (System.Drawing.Common 4.7.2, Skia) no chocan con las de la API (System.Drawing.Common 10, fijada por
    /// seguridad). <c>Impresion</c> sí se comparte con la API para que la interfaz <see cref="IGeneradorPdf"/> sea la misma.
    /// </summary>
    internal sealed class CargadorGeneradorPdf : AssemblyLoadContext
    {
        private readonly string _carpeta;

        private CargadorGeneradorPdf(string carpeta) : base("generador-pdf", isCollectible: false)
        {
            _carpeta = carpeta;
        }

        public static IGeneradorPdf Crear(string carpeta, string nombreTipo, params object?[] argumentos)
        {
            carpeta = Path.GetFullPath(carpeta);
            var contexto = new CargadorGeneradorPdf(carpeta);
            var ensamblado = contexto.LoadFromAssemblyPath(Path.Combine(carpeta, "Impresion.DevExpress.dll"));
            var tipo = ensamblado.GetType(nombreTipo, throwOnError: true)!;
            return (IGeneradorPdf)Activator.CreateInstance(tipo, argumentos)!;
        }

        protected override Assembly? Load(AssemblyName nombre)
        {
            // Lo que la API ya tiene (y debe compartir) se deja al contexto por defecto.
            if (nombre.Name is null || nombre.Name == "Impresion") return null;

            var ruta = Path.Combine(_carpeta, nombre.Name + ".dll");
            return File.Exists(ruta) ? LoadFromAssemblyPath(ruta) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string nombre)
        {
            var archivo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? nombre + ".dll" : "lib" + nombre + ".so";
            foreach (var candidato in new[] { Path.Combine(_carpeta, nombre), Path.Combine(_carpeta, archivo) })
            {
                if (File.Exists(candidato)) return LoadUnmanagedDllFromPath(candidato);
            }

            return IntPtr.Zero;
        }
    }
}
