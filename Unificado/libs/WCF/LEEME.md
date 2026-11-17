# DLL propia de WCF (`System.Private.ServiceModel.dll`)

## Por qué existe

El servicio web de la DIAN (`WcfDianCustomerServices.svc`) exige la suite de seguridad `Basic256Sha256Rsa15`. El paquete oficial `System.ServiceModel` de .NET no la trae. Esta DLL sale de un fork de [dotnet/wcf](https://github.com/dotnet/wcf) que la agrega. `ServiciosWeb.csproj` la referencia por `HintPath`.

## Qué es exactamente

| Dato | Valor |
|---|---|
| Archivo | `libs\WCF\System.Private.ServiceModel.dll` |
| Versión | 5.0.24.27801 |
| Compilada | mayo de 2024 |
| SHA-256 | `79d09962c1eb9c6ae3d5c0a906a03e1bb6de4e9a34e548139f74d175ccc6dbb6` |
| Fuente | `PosDocumentoSoporte\eFacturacionColombia_NETCore\WCF\wcf` (fork de dotnet/wcf) |

Archivos modificados en el fork (ambos bajo `src\System.Private.ServiceModel\src\System\ServiceModel\Security\`):

- `SecurityAlgorithmSuite.cs`: agrega `Basic256Sha256Rsa15`.
- `WSSecurityPolicy.cs`: la reconoce al leer la política de seguridad.

Para ver la diferencia exacta, compare esos dos archivos con la misma versión de la rama `release` de dotnet/wcf. El fork no es un repositorio git, así que no hay historial que la muestre.

## Cómo reconstruirla

1. **No compile dentro de `PosDocumentoSoporte`**: copie la carpeta `WCF\wcf` a un directorio temporal. Esa carpeta es del proyecto antiguo y no debe cambiar.
2. En la copia, siga el procedimiento de compilación de dotnet/wcf (`build.cmd` o `build.sh`, en `eng\`). Necesita el SDK que pide `global.json` de esa carpeta, que es antiguo, y es probable que haya que instalarlo aparte.
3. El resultado queda en `artifacts\bin\System.Private.ServiceModel\...`. Copie la DLL a `libs\WCF`.
4. Anote el nuevo SHA-256 en esta tabla y verifique con la herramienta de habilitación (`enviar-sync` de una factura) que la DIAN responde.

No se intentó la reconstrucción el 2026-11-07: la DLL actual funciona en Windows y en Linux (ver `docs\fase1-bitacora-migracion.md`).

## Riesgos y salidas

- **Mantenimiento:** es una DLL interna de WCF compilada contra una versión vieja (5.0). Una actualización de .NET puede romper la compatibilidad. Las pruebas de habilitación son la alarma.
- **Salida a largo plazo:** proponer la suite a dotnet/wcf, o dejar WCF y llamar al servicio como SOAP crudo con `HttpClient` y firma WS-Security propia. Queda como decisión abierta.
