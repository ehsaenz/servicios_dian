# Subir la configuración de un cliente existente (importar)

Última revisión: 2026-11-16 · Rol: **operador con acceso al servidor (SSH)**

Si el cliente ya existía en la API anterior de POS / documento soporte, tiene un archivo de configuración (`ConfigAPI*.json`) y su certificado. En lugar de llenar las pantallas a mano, se importa **por comando**. No hay una pantalla para esto: se hace desde el servidor, y por eso los secretos (PIN y clave del certificado) no pasan por un navegador.

## Qué hace

Con un solo comando crea el cliente, guarda su software (cifrando los PIN), valida y guarda su certificado y, si se pide, genera la primera clave de API. Valida los módulos y el certificado antes del alta. **No es una transacción global**: un fallo de escritura posterior puede dejar un cliente parcial; revise el resultado antes de repetir.

## El archivo de configuración

Un JSON con estas secciones (valores ficticios):

```json
{
  "Emisor": { "Nit": "900123456", "DV": "7", "RazonSocial": "EMPRESA EJEMPLO SAS" },
  "Certificado": { "FileName": "certificado-ejemplo.p12" },
  "Modulos": [
    { "TipoSoftware": "DocumentoSoporte", "Identificador": "00000000-0000-0000-0000-000000000000", "PIN": "<pin-del-software>", "AmbientePruebas": true, "TestSetID": "<testsetid>" }
  ]
}
```

- **Emisor** es obligatorio.
- **Certificado.FileName** es el `.p12`, **en la misma carpeta que el archivo JSON**.
- **Modulos** lista los software (uno por tipo). Si el archivo trae un solo `Software` (formato antiguo), se usa para los dos tipos que atendía la API anterior: documento soporte y documento equivalente POS.
- Cada software necesita un **Identificador con forma de GUID** y un **PIN**.
- Los nombres de campo no distinguen mayúsculas de minúsculas.

## Comando

En el servidor (por SSH). La clave del certificado se entrega por una **variable de entorno**, no por argumento, para que no quede en el historial:

```bash
read -rs CLAVE_P12 && export CLAVE_P12
sudo --preserve-env=CLAVE_P12 dian-admin importar-cliente <id> /ruta/config.json --clave-cert-env=CLAVE_P12 --generar-clave=true
unset CLAVE_P12
```

- `<id>`: el id del cliente (mismas reglas que en [crear-cliente.md](crear-cliente.md)).
- `--clave-cert-env=NOMBRE`: nombre de la variable con la clave del `.p12`. Si no se indica, se toma la del archivo (`Certificado.Clave`); no se recomienda tener la clave en el archivo.
- `--generar-clave=true`: crea una clave de API con descripción «Importado».
- Escriba las opciones propias **con signo igual** (`--opcion=valor`), no con espacio.

El comando `dian-admin` corre como el usuario del servicio y lee `/etc/dian/dian.env`. El usuario `dian` debe poder **leer** el archivo `.p12` y el JSON en la ruta que indique.

### Resultado

- Éxito: «Cliente <id> importado.» y, si pidió clave, una última línea `CLAVE_API=...`. **Esa clave se muestra una sola vez**: cópiela y entréguela por un canal seguro ([claves-api.md](claves-api.md)).
- Error: «No se pudo importar: …» con el motivo. Mensajes frecuentes:

| Mensaje | Qué hacer |
|---|---|
| «El archivo no trae la sección Emisor.» | Agregue `Emisor` al JSON |
| «El software … necesita un identificador GUID y un PIN.» | Corrija el módulo señalado |
| «Falta la clave del certificado (variable de entorno o archivo).» | Defina la variable y pásela con `--clave-cert-env` |
| «No se pudo abrir el certificado: el archivo no es un .p12 válido o la clave es incorrecta.» | Revise el archivo y la clave |
| «El certificado está vencido desde …» | Pida un certificado nuevo |
| «Ya existe un cliente con el id …» | No sobrescribe; use otro id o edite desde el panel |

## Después de importar

1. Entre al panel y abra el cliente. Revise **Emisor**, el software y el **Certificado de firma** (sujeto y vencimiento).
2. Si el cliente va a emitir **factura electrónica**, el archivo antiguo no trae ese tipo: registre su bloque **Software · Facturación electrónica** a mano ([configurar-cliente.md](configurar-cliente.md)).
3. Complete el emisor (correo, teléfono, matrícula), sucursales y logo.

## No verificado

- La combinación exacta `sudo --preserve-env=... dian-admin importar-cliente` no se ha probado tal cual; el piloto del 2026-11-07 usó el mismo comando sin `sudo`, con la variable exportada. Si `sudo` no deja pasar la variable, hágalo como usuario con permiso o pida apoyo al equipo de desarrollo.

- Un fallo a mitad de la importación (después de validar) puede dejar el cliente a medias; si ocurre, avise al equipo de desarrollo: debe inspeccionarse qué se guardó y preparar una recuperación controlada, sin borrar datos de un cliente que ya opere.
- El formato exacto del archivo de configuración de la API anterior se describe aquí por el código que lo lee; no se probó con archivos de todos los clientes.
