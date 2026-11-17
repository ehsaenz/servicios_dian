# Prueba de DIAN.Unificado en Linux

Este paquete contiene las herramientas de prueba de las bibliotecas de facturación electrónica nuevas (.NET 10), compiladas para `linux-x64` y **autocontenidas**: no hay que instalar .NET en el servidor.

Su objetivo es comprobar en Linux lo que ya funciona en Windows: generar, firmar y enviar documentos a la **habilitación** de la DIAN. Nunca envía a producción.

## Qué contiene

| Archivo | Para qué sirve |
|---|---|
| `Habilitacion.Pruebas` | Genera, firma y envía a habilitación factura, notas, documento soporte, nómina, eventos RADIAN y documento equivalente POS. |
| `FirmaXades.Pruebas` | Revisa un certificado y verifica la firma de un XML de forma independiente. |
| `probar-linux.sh` | Script que ejecuta la prueba completa y deja el resultado en `resultado-linux.txt`. |
| `System.Private.ServiceModel.dll` | WCF propio que usa la DIAN para la suite `Basic256Sha256Rsa15`. Es el punto que más conviene probar en Linux. |
| `muestra/` | XML de ejemplo que usa `FirmaXades.Pruebas`. |

El paquete **no incluye** el certificado, la clave ni los datos de habilitación (software, PIN, set de pruebas). Hay que ponerlos aparte.

## Requisitos del servidor

- Linux de 64 bits con glibc (Ubuntu, Debian, CentOS, Rocky, etc.). Si usas Alpine, hace falta otro paquete.
- Salida a internet hacia `vpfe-hab.dian.gov.co` por el puerto 443.
- Paquete `tzdata` (zona horaria `America/Bogota`).
- Biblioteca ICU (`libicu`). Si falta y .NET no arranca, define `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

## Pasos

1. **Copia el paquete** al servidor y descomprímelo:

   ```bash
   mkdir -p ~/dian-pruebas && tar -xzf DIAN.Unificado.Pruebas.linux-x64.tar.gz -C ~/dian-pruebas
   cd ~/dian-pruebas
   chmod +x probar-linux.sh Habilitacion.Pruebas FirmaXades.Pruebas
   ```

2. **Crea la carpeta `config-local`** junto a los ejecutables y copia ahí, desde `Unificado\config-local` del equipo Windows, estos archivos (por un canal seguro, por ejemplo `scp`):

   - `certificado.p12`
   - `emisor-marlit.json`, `habilitacion-sw-propio.json`, `emisor-rut.json`
   - `documento-soporte.json`, `nomina.json`, `pos.json`

   La ruta del certificado dentro de `emisor-marlit.json` es de Windows; no hace falta cambiarla: si no existe, la herramienta busca `certificado.p12` dentro de `config-local`.

   Restringe los permisos: `chmod 700 config-local && chmod 600 config-local/*`.

3. **Define la clave del certificado** solo en tu sesión, sin dejarla en el historial:

   ```bash
   read -rs MARLIT_P12 && export MARLIT_P12
   ```

4. **Mira el entorno** (no usa el certificado ni la red):

   ```bash
   ./probar-linux.sh diagnostico
   ```

5. **Prueba local**: genera y firma cuatro documentos y verifica sus firmas. No envía nada.

   ```bash
   ./probar-linux.sh local
   ```

6. **Prueba contra habilitación** (opcional, pide que escribas `SI`): además envía cuatro documentos de prueba a la habilitación de la DIAN.

   ```bash
   ./probar-linux.sh habilitacion
   ```

7. **Comparte `resultado-linux.txt`.** Contiene el diagnóstico y la salida de cada paso, sin claves. Incluye el nombre del titular del certificado.

Al terminar, borra la clave de la sesión con `unset MARLIT_P12`.

## Qué se espera y qué puede fallar

Si todo va bien, `local` termina con "0 con fallas" y `habilitacion` muestra `Procesado Correctamente` en cada envío.

Los puntos de riesgo, por orden de probabilidad:

1. **WCF propio** (`System.Private.ServiceModel.dll`): podría fallar al enviar a la DIAN con un error de tipo o de plataforma no soportada. Es lo único que solo se ejercita en el envío.
2. **Carga del certificado `.p12`**: podría fallar según la versión de OpenSSL del servidor.
3. **Zona horaria**: si el servidor no está en `America/Bogota`, revisa que la hora de los documentos sea la colombiana (`-05:00`). El diagnóstico lo avisa.
4. **ICU o `tzdata` ausentes**: el diagnóstico los detecta.

## Consecutivos

Los números de prueba que usa el script son los siguientes libres en habilitación de MARLIT: factura `SETP990000509`, documento soporte `SEDS984000507`, documento equivalente `EPOS4` y nómina `NOM1003`. Si los repites, la DIAN rechaza por documento ya procesado y habrá que subirlos.
