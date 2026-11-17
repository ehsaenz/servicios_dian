# Llaves SSH con frase de seguridad (GitHub y GitLab)

Guía para configurar en Windows las llaves SSH que usamos para hacer `git push` y `git pull`, protegidas con frase de seguridad y cargadas en el agente SSH de Windows.

> Esta guía no contiene llaves ni frases. Nunca copie la llave **privada** (el archivo sin `.pub`) a un chat, un correo, un repositorio ni a la nube.

## 1. Idea general

- Cada servicio (GitHub, GitLab, un servidor) tiene **su propia llave**. Así, si una se compromete o se pierde, solo afecta a ese servicio.
- La llave privada se protege con una **frase de seguridad** (passphrase). Sin ella, quien copie el archivo puede usar la llave.
- Para no escribir la frase en cada `git push`, la llave se carga una vez en el **agente SSH de Windows** (`ssh-agent`). El agente guarda la llave descifrada en memoria mientras el servicio esté corriendo.
- La llave pública (`.pub`) es la que se registra en el servicio. Es segura de compartir.

## 2. Convención de nombres

Nombre de la llave: `<servicio>-<cliente o contexto>`, todo en minúsculas y con guion medio.

| Llave (archivo) | Uso | Comentario (lo que muestra `ssh-add -l`) |
|---|---|---|
| `github-sis360` | GitHub | `github-sis360` |
| `gitlab-sis360` | GitLab SIS360 | `gitlab-sis360` |
| `gitlab-poblado` | GitLab (cliente Poblado) | `gitlab-poblado` |

Reglas:

- Use **guion medio** (`-`), no guion bajo. Al pasar la ruta en la terminal, un nombre con guion bajo distinto del archivo real da el error `No such file or directory`.
- El **comentario** (la etiqueta que muestra `ssh-add -l`) debe ser igual al nombre de la llave. Así no hay que recordar a qué servicio pertenece cada una.
- No use correos personales como comentario: `ssh-add -l` muestra el comentario y, si todas las llaves dicen lo mismo, no se distinguen.

## 3. Crear una llave nueva

En PowerShell, en la carpeta `%USERPROFILE%\.ssh`:

```powershell
ssh-keygen -t ed25519 -C "github-sis360" -f "$env:USERPROFILE\.ssh\github-sis360"
```

- Cuando pida la frase de seguridad, use una frase **distinta** para cada llave y guárdela en el gestor de contraseñas del equipo.
- Se generan dos archivos: `github-sis360` (privada) y `github-sis360.pub` (pública).

## 4. Registrar la llave pública en el servicio

Copie el contenido de `github-sis360.pub` (una línea que empieza por `ssh-ed25519`):

```powershell
Get-Content "$env:USERPROFILE\.ssh\github-sis360.pub"
```

- **GitHub:** Settings → SSH and GPG keys → New SSH key.
- **GitLab:** Preferences → SSH Keys → Add new key.

El título de la llave en el servicio puede ser el mismo nombre. Registre solo la `.pub`.

## 5. Archivo `~/.ssh/config`

Cada servicio tiene una entrada que indica qué llave usar. Ejemplo (en `%USERPROFILE%\.ssh\config`):

```
Host github.com
  HostName github.com
  User git
  IdentityFile ~/.ssh/github-sis360
  IdentitiesOnly yes
  AddKeysToAgent yes

Host gitlab-sis360
  HostName gitlab.com
  User git
  IdentityFile ~/.ssh/gitlab-sis360
  IdentitiesOnly yes
  AddKeysToAgent yes
```

- `IdentitiesOnly yes` evita que SSH pruebe otras llaves y se confunda.
- El nombre después de `Host` es el alias que se usa en las URL (`git@gitlab-sis360:grupo/proyecto.git`). Para GitHub se usa `github.com`.

## 6. Activar el agente SSH de Windows (una sola vez)

El servicio `ssh-agent` de Windows debe estar en inicio automático. Requiere PowerShell **como administrador**:

```powershell
Set-Service ssh-agent -StartupType Automatic
Start-Service ssh-agent
Get-Service ssh-agent
```

Debe aparecer `Running`. Si aparece `Acceso denegado`, la ventana no está como administrador.

## 7. Cargar las llaves en el agente

Hay que hacerlo una vez por sesión de Windows (después de reiniciar, si el agente no conserva las llaves). Use el `ssh-add` de Windows, no el de Git Bash:

```powershell
& "$env:WINDIR\System32\OpenSSH\ssh-add.exe" "$env:USERPROFILE\.ssh\github-sis360"
& "$env:WINDIR\System32\OpenSSH\ssh-add.exe" "$env:USERPROFILE\.ssh\gitlab-sis360"
```

Pedirá la frase de seguridad de cada llave. Después, verifique:

```powershell
& "$env:WINDIR\System32\OpenSSH\ssh-add.exe" -l
```

Debe listar una línea por llave, con su nombre como comentario.

## 8. Probar la conexión

```powershell
ssh -T git@github.com
ssh -T git@gitlab-sis360
```

Respuestas correctas:

- GitHub: `Hi <usuario>! You've successfully authenticated, but GitHub does not provide shell access.` El código de salida es 1; es normal.
- GitLab: `Welcome to GitLab, @<usuario>!`.

## 9. Git usa el cliente SSH de Windows

Git for Windows trae su propio `ssh`, que **no** consulta el agente del servicio de Windows. Si `git push` responde `Permission denied (publickey)` aunque `ssh -T` funcione, configure el repositorio para usar el OpenSSH de Windows:

```powershell
git config core.sshCommand "C:/Windows/System32/OpenSSH/ssh.exe"
```

Es un ajuste por repositorio. Se puede poner en el `.gitconfig` global si todos los repos de la máquina usan SSH.

Las URL de los repositorios deben ser SSH (`git@github.com:...` o `git@gitlab-sis360:...`), no HTTPS.

## 10. Problemas frecuentes

| Mensaje | Causa probable | Qué hacer |
|---|---|---|
| `Error connecting to agent: No such file or directory` | El servicio `ssh-agent` no está iniciado, o se usa el `ssh-add` de Git Bash. | Iniciar el servicio (sección 6) y usar el `ssh-add` de Windows. |
| `Set-Service: Acceso denegado` | PowerShell no está como administrador. | Abrirlo como administrador. |
| `ssh-add: No such file or directory` | El nombre de la llave no coincide con el archivo. | Revisar con `Get-ChildItem "$env:USERPROFILE\.ssh"`. |
| `Permission denied (publickey)` | La llave no está cargada en el agente, o no está registrada en el servicio. | Cargarla (sección 7) y comprobar que la `.pub` está en el servicio. |
| `Host key verification failed` | Falta la huella del servidor en `known_hosts`. | Confirmar la huella publicada por el servicio antes de aceptarla. |
| `git push` falla aunque `ssh -T` funcione | Git usa su propio cliente SSH. | Sección 9. |

## 11. Qué no hacer

- No quitar la frase de seguridad para "simplificar". Si la llave se copia, quien la tenga puede usarla.
- No compartir la llave privada, ni por chat, ni en el repositorio, ni en capturas de pantalla.
- No usar la misma llave para dos servicios. Un compromiso afectaría a ambos.
- No escribir las frases en archivos de texto del equipo. Use el gestor de contraseñas.
- No subir `~/.ssh/config` a un repositorio si contiene nombres de clientes o servidores internos.

## 12. Rotar una llave

1. Crear una llave nueva (sección 3) con otro nombre o con sufijo de año, por ejemplo `github-sis360-2027`.
2. Registrar su `.pub` en el servicio (sección 4).
3. Actualizar `IdentityFile` en `config` y cargarla en el agente (sección 7).
4. Probar con `ssh -T` y con un `git push` a un repositorio de prueba.
5. Eliminar la llave anterior del servicio y, cuando ya no se use, borrar sus dos archivos del disco.

## 13. Qué no se ha verificado

- Que el agente conserve las llaves después de reiniciar Windows en todos los equipos del equipo. Debe comprobarse en cada máquina con `ssh-add -l`.
- El acceso desde servidores Linux a GitHub o GitLab con las mismas llaves. Eso requiere llaves propias del servidor.
