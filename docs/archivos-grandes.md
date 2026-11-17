# Inventario de archivos grandes y exclusiones Git

Revisión: 2026-11-16. Tamaños lógicos de archivos medidos en disco; pueden diferir del espacio asignado que muestra Windows.

## Resultado

- Directorio completo medido: **5.50 GiB**.
- Archivos candidatos a Git después de excluir `LOG/`: **129.3 MiB** (sin compresión Git).
- No hay commits ni archivos en el índice en el momento de esta revisión. No hace falta `git rm --cached` para aplicar estas exclusiones.
- `.gitignore` evita versionar archivos; **no reduce el tamaño de la carpeta en disco**.
- No se eliminaron archivos ni se modificaron dependencias o recursos de los formularios.

## Dónde está el espacio

Cada archivo se cuenta una vez en esta tabla. Las carpetas anidadas se asignan al primer grupo reconocido de su ruta.

| Grupo | MiB | Acción |
|---|---:|---|
| `bin` | 2498.0 | Ya excluido. Normalmente regenerable al compilar; revisar referencias del legado antes de limpiar. |
| `packages` | 980.1 | Ya excluido. Regenerable con restauración NuGet si los paquetes y fuentes siguen disponibles. |
| `dist` | 635.7 | Ya excluido. Publicaciones y paquetes; conservar versiones necesarias para despliegue o reversión. |
| `otros` | 624.9 | Código, recursos, dependencias y otros archivos; no es una categoría de borrado. |
| `.git-backups` | 421.0 | Ya excluido. Historial anterior; trasladar a almacenamiento privado antes de retirarlo de este equipo. |
| `obj` | 334.9 | Ya excluido. Regenerable al compilar. |
| `.vs` | 90.5 | Ya excluido. Caché del IDE; cerrar Visual Studio antes de una limpieza. |
| `.git` | 49.1 | Metadatos del repositorio actual. No borrar manualmente sus objetos. |

`bin` + `obj` + `packages` + `.vs` representan aproximadamente **3.81 GiB** de artefactos y cachés. Limpiarlos físicamente es una operación distinta de excluirlos de Git; en este trabajo solo se midieron.

## Archivos grandes que ya están excluidos

| Archivo | MiB |
|---|---:|
| `.git-backups/consolidacion-2026-11-16/FacturacionElectronica.git/objects/pack/pack-c897b4002ba0e9b1d00c3972399779919dddb5bd.pack` | 340.8 |
| `Unificado/dist/servicios-linux-x64.tar.gz` | 128.0 |
| `FacturacionElectronica/EnviosDian/EnviosDian/EnviosDian-SetupFiles/EnviosDian.msi` | 53.8 |
| `FacturacionElectronica/EnviosDian/EnviosDian/EnviosDian-cache/part1/disk1.cab` | 52.3 |
| `Unificado/dist/DIAN.Unificado.Pruebas.linux-x64.tar.gz` | 37.1 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/obj/Debug/EnvioFacturacion_Dian.exe` | 28.8 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/bin/Debug/app.publish/EnvioFacturacion_Dian.exe` | 28.8 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/bin/Debug/EnvioFacturacion_Dian.exe` | 28.8 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/obj/Release/EnvioFacturacion_Dian.exe` | 28.6 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/bin/Release/app.publish/EnvioFacturacion_Dian.exe` | 28.6 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/bin/Release/EnvioFacturacion_Dian.exe` | 28.6 |
| `NominaRadian.Bibliotecas/OLD.zip` | 26.5 |
| `.git-backups/consolidacion-2026-11-16/NominaRadian.Bibliotecas.git/objects/04/4adfa30e5f887482f886f22572c824a7b289e4` | 26.2 |
| `Unificado/docs/Caja_de_herramientas_Documento_Soporte_Validacion_Previa.zip` | 26.1 |
| `FacturacionElectronica/Excel.zip` | 25.2 |
| `Unificado/docs/Caja-de-herramientas-FE_V19_v2026.zip` | 25.0 |
| `.git-backups/consolidacion-2026-11-16/NominaRadian.Api.git/objects/pack/pack-17d5abab0fb098f7f165cdef497ef42c2c7ed83c.pack` | 23.6 |

## Mayores archivos que sí se incluirían

| Archivo | MiB |
|---|---:|
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/Formularios/Admin/RbfMenuGeneral.resx` | 21.03 |
| `FacturacionElectronica/EnviosDian/EnvioFacturacion_Dian/Formularios/Admin/XtraAcercaDe.resx` | 21.00 |
| `NominaRadian.Api/Dependencias/iTextSharp.LGPLv2.Core.dll` | 8.44 |
| `FacturacionElectronica/DEPENDENCIAS/itextsharp.dll` | 3.89 |
| `FacturacionElectronica/DEPENDENCIAS/itextsharp.xml` | 3.03 |
| `NominaRadian.Api/Dependencias/System.Private.ServiceModel.dll` | 2.91 |
| `Unificado/libs/WCF/System.Private.ServiceModel.dll` | 2.91 |
| `PosDocumentoSoporte/eFacturacionColombia_NETCore/DEPENDENCIAS/System.Private.ServiceModel.dll` | 2.91 |
| `PosDocumentoSoporte/eFacturacionColombia_NETCore/BINARIOS/netstandard2.0/System.Private.ServiceModel.dll` | 2.91 |
| `NominaRadian.Bibliotecas/Libs/System.Private.ServiceModel.dll` | 2.88 |
| `NominaRadian.Api/Dependencias/BouncyCastle.Crypto.dll` | 2.74 |
| `FacturacionElectronica/DEPENDENCIAS/BouncyCastle.Crypto.dll` | 2.41 |
| `FacturacionElectronica/03_FE_V2_Library/eFacturacionColombia_V2/EstructurasXmlPdf.cs` | 2.02 |
| `FacturacionElectronica/03_FE_V2_Library/eFacturacionColombia_V2/Documentos/Xml/XmlDianUBL.cs` | 2.01 |
| `Unificado/src/Shared/Tipos/Standard.cs` | 2.01 |
| `NominaRadian.Bibliotecas/eFacturacionColombia_NETCore.Tipos/Standard.cs` | 2.01 |
| `PosDocumentoSoporte/eFacturacionColombia_NETCore/eFacturacionColombia_NETCore.Tipos/Standard.cs` | 1.95 |
| `FacturacionElectronica/00_CG/XML-InvoiceClass.cs` | 1.76 |

## Qué conviene optimizar

1. **Los dos RESX de formularios (42 MiB entre ambos).** `RbfMenuGeneral.resx` contiene un valor Base64 de 21.771.841 caracteres en `PtbLogo.EditValue`; `XtraAcercaDe.resx` contiene otro del mismo tamaño en `pictureEdit1.EditValue`. Casi todo el peso viene de esas imágenes. Se puede evaluar una imagen de menor resolución o compresión y un recurso compartido, verificando los formularios en Visual Studio. No se cambiaron porque afectaría recursos de la aplicación.
2. **Dependencias binarias.** No excluir globalmente DLL ni `BINARIOS`: existen referencias `HintPath` en los proyectos antiguos. Migrarlas a NuGet o referencias de proyecto es trabajo de compilación aparte. La DLL WCF de Unificado es una dependencia explícita.
3. **Documentación XML de DLL.** Por ejemplo, `DEPENDENCIAS/itextsharp.xml` ocupa unos 3 MiB. Puede estudiarse distribuir estos archivos con las dependencias, pero no se excluyeron indiscriminadamente los XML: el proyecto también utiliza XML funcional.
4. **Historial anterior.** El mayor archivo individual es un paquete Git de unos 341 MiB dentro del respaldo de FacturacionElectronica. Ya está excluido. Su traslado fuera de DIAN reduce el tamaño local sin afectar el nuevo repositorio, pero debe conservarse si se quiere recuperar el historial.

## Cambio aplicado

Se añadió `[Ll][Oo][Gg]/` al `.gitignore` raíz para excluir carpetas de registros, incluida `NominaRadian.Api/API_NOMINA/API_NOMINA/LOG/log.txt`. ZIP, bin, obj, `.obj`, temporales, paquetes y respaldos ya estaban excluidos.

## Comprobar antes de versionar

```powershell
git ls-files --others --exclude-standard
git ls-files -ci --exclude-standard
git check-ignore -v -- NominaRadian.Api/API_NOMINA/API_NOMINA/LOG/log.txt
```

El segundo comando debe quedar sin resultados: detecta archivos ya rastreados que las reglas actuales excluirían. El inventario técnico completo de esta medición está en `.git-backups/analisis-tamanos.json`, fuera del repositorio.
