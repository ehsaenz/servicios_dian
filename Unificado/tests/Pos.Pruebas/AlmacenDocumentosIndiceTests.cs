using System;
using System.IO;
using DIAN.Pos.Servicios.Documentos;
using Xunit;

namespace Pos.Pruebas;

public class AlmacenDocumentosIndiceTests
{
    [Fact]
    public void Las_lecturas_son_copias_y_el_indice_se_recarga_desde_el_disco()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "almacen-indice-" + Guid.NewGuid().ToString("N"));
        try
        {
            var almacen = new AlmacenDocumentos(carpeta);
            almacen.Guardar(new DocumentoRegistro { Numero = "SETP1", TipoDocDian = "01", Estado = EstadoDocumento.Recibido });

            // Un cambio sobre la copia leída no altera el índice mientras no se guarde.
            var copia = almacen.Buscar("SETP1")!;
            copia.Estado = EstadoDocumento.Aprobado;
            Assert.Equal(EstadoDocumento.Recibido, almacen.Buscar("SETP1")!.Estado);

            // Un almacén nuevo sobre la misma carpeta carga lo guardado desde los archivos.
            var recargado = new AlmacenDocumentos(carpeta);
            Assert.Equal("SETP1", recargado.Buscar("SETP1")!.Numero);

            // El cursor de cambios se resuelve desde el índice.
            Assert.Single(almacen.CambiosDesde(0, 10));
            Assert.Empty(almacen.CambiosDesde(almacen.Buscar("SETP1")!.SecuenciaCambio, 10));
        }
        finally
        {
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
        }
    }
}
