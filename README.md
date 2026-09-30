# Sistema de Gestión de Citas Médicas

Proyecto de Programación 2 en el ITLA. Es un programa en C# para que una clínica lleve el control de sus pacientes, médicos y citas.

## ¿Por qué lo hice?

La clínica del ejercicio maneja las citas a mano: el paciente llama, la recepcionista revisa si el médico está libre y anota todo en una hoja de cálculo. Así es fácil que se dupliquen citas, que una cancelación quede mal anotada o que nadie avise al paciente el día anterior.

Este programa reemplaza esa hoja de cálculo con algo más ordenado, y lo hice pensando en que sea fácil de entender y de ampliar después.

## ¿Qué se puede hacer?

- Registrar pacientes, médicos y especialidades
- Asignar un médico a una especialidad
- Agendar una cita (el programa no deja que un médico tenga dos citas a la misma hora)
- Ver las citas de un paciente o de un médico
- Cancelar o reprogramar una cita
- Enviar un recordatorio al paciente

## Cómo probarlo

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download). Luego:

```bash
git clone <URL-de-este-repositorio>
cd SistemaCitasMedicas
dotnet run --project src/SistemaCitasMedicas
```

Si prefieres Visual Studio, abre el archivo `SistemaCitasMedicas.sln` y presiona `Ctrl + F5`.

Un par de detalles para usarlo:

- Las fechas se escriben así: `2026-10-15 09:30`
- Cada cita dura 30 minutos
- Los datos se guardan en memoria, así que se borran al cerrar el programa

## Cómo está organizado

Separé el código en cuatro carpetas para que cada cosa esté en su lugar:

```text
src/SistemaCitasMedicas/
├── Domain/          Los datos del negocio: Paciente, Medico, Especialidad, Cita
├── Application/     Las reglas y los servicios (agendar, cancelar, validar...)
├── Infrastructure/  Lo que toca el "exterior": guardar datos, enviar correos, la hora
├── Presentation/    El menú de consola
└── Program.cs       Donde se arma todo
```

La idea principal es que las reglas de la clínica no dependen de cómo se guardan los datos ni de cómo se manda un correo. Si mañana se cambia la memoria por una base de datos, las reglas no se tocan.

## Principios que apliqué

El ejercicio pedía SoC, DRY, KISS, YAGNI y SOLID. En resumen:

| Principio | Cómo lo apliqué |
|---|---|
| **SoC** | Cada capa tiene su trabajo. El menú no tiene reglas y los servicios no saben cómo se guardan los datos. |
| **DRY** | Las validaciones están en un solo lugar y la regla de disponibilidad se usa igual al agendar y al reprogramar. |
| **KISS** | Sin bases de datos ni frameworks extra. Una especialidad por médico y citas de duración fija. |
| **YAGNI** | Solo hice lo que pidió la clínica, nada más. |
| **SRP** | Cada clase hace una sola cosa. No existe una clase gigante que haga todo. |
| **OCP** | Agregar un nuevo tipo de recordatorio no obliga a cambiar el código que ya existe. |
| **LSP** | Paciente y médico son "personas" y se pueden usar donde se pida una persona. |
| **ISP** | Las interfaces son pequeñas, con pocos métodos. |
| **DIP** | Los servicios trabajan con interfaces, no con clases concretas. |

## Agregar otro tipo de recordatorio

Ahora los recordatorios se mandan por correo, pero el sistema está preparado para SMS, WhatsApp o notificaciones del celular. Solo hay que hacer dos cosas.

1. Crear una clase nueva:

```csharp
public class SmsNotificador : INotificador
{
    public string Canal => "SMS";

    public void Enviar(Paciente paciente, string mensaje)
    {
        // aquí iría el envío del SMS usando paciente.Telefono
    }
}
```

2. Agregarla en `Program.cs`:

```csharp
INotificador[] notificadores = { new EmailNotificador(Console.Out), new SmsNotificador() };
```

No hay que modificar nada más.

## Lo que dejé fuera a propósito

No incluí facturación, recetas, historial clínico, seguros, telemedicina, chat médico ni inteligencia artificial. La clínica no lo pidió, y cada una de esas cosas es un proyecto aparte. Es mejor tener algo pequeño que funcione bien y crecer después.

## Justificación técnica

**1. ¿Por qué dividí el sistema en esos módulos?**

Porque cada módulo es una cosa distinta del negocio y cambia por sus propias razones: pacientes, médicos, especialidades, citas y recordatorios. Además coinciden con lo que pidió la clínica, así que cada requerimiento tiene un lugar claro. También separé dos cosas que se usan en todas partes, las validaciones y el guardado de datos, para no repetirlas. Así, si tengo que cambiar algo de las citas, no toco pacientes ni médicos.

**2. ¿Por qué esas clases son necesarias?**

- **Paciente, Medico, Especialidad y Cita:** son los datos del negocio. Hice una clase `Persona` para que paciente y médico no repitan nombre, apellido, teléfono y correo.
- **Un servicio por módulo:** para que cada clase tenga un solo trabajo y no exista una clase gigante que haga todo.
- **Validadores:** para tener las reglas de los datos en un solo sitio.
- **VerificadorDisponibilidad:** evitar citas duplicadas es lo más importante que pidió la clínica, así que merecía su propia clase.
- **INotificador y EmailNotificador:** para mandar recordatorios hoy por correo y mañana por otros medios.
- **Repositorios y IReloj:** para no depender de cómo se guardan los datos ni de la hora real del computador.

**3. ¿Qué clases o funcionalidades decidí no crear y por qué?**

No hice facturación, recetas, historial clínico, seguros, telemedicina, chat ni inteligencia artificial, porque la clínica no los pidió y cada uno es prácticamente un proyecto aparte. Tampoco creé cosas que parecían útiles pero que hoy no hacen falta: una clase de horarios del médico (la disponibilidad se saca de las citas que ya existen), usuarios con inicio de sesión, ni un contenedor de inyección de dependencias. Y aunque el sistema está listo para SMS o WhatsApp, no los programé todavía porque nadie los ha pedido.

**4. ¿Dónde podría aparecer código repetido?**

En las validaciones (campos vacíos, fechas, datos obligatorios y disponibilidad), en los datos de pacientes y médicos que son casi iguales, en la búsqueda de algo por su id que hacen todos los servicios, y en el guardado de cada entidad. Lo evité con una clase `Reglas` que tiene todas las validaciones, un validador de personas que sirve para pacientes y médicos, el `VerificadorDisponibilidad` que se usa al agendar y al reprogramar, un método `ObtenerObligatorio` para buscar por id y un repositorio genérico que sirve para todas las entidades.

**5. ¿Cómo se podría extender el sistema en el futuro?**

- **Otro tipo de recordatorio:** se crea una clase que use `INotificador` y se agrega en `Program.cs`. No se toca nada más.
- **Base de datos:** se crea un repositorio nuevo que use la misma interfaz y los servicios no se enteran del cambio.
- **Módulos nuevos como facturación o historial:** se crean como clases aparte que se relacionan con la cita o el paciente por su id.
- **Una versión web o móvil:** se hace una presentación nueva que reutilice los mismos servicios.

**6. ¿Qué principio fue más difícil de aplicar y por qué?**

El más difícil fue balancear OCP con YAGNI. OCP te pide dejar todo preparado para crecer, pero YAGNI te pide no construir lo que todavía no se necesita. Si preparas todo "por si acaso", terminas con un sistema complicado que rompe KISS. Mi criterio fue crear una abstracción solo donde ya se sabe que habrá cambios: los recordatorios (la asignación lo pide), el guardado de datos (hoy es en memoria, mañana será una base de datos) y la hora (para poder validar fechas). Todo lo demás lo dejé simple.

## Preguntas de reflexión

**1. ¿Qué pasaría si todo el sistema se implementa en una sola clase?**

Sería una clase enorme, difícil de leer y de cambiar. Cualquier arreglo pequeño podría dañar otra cosa que no tiene nada que ver, no se podría cambiar una parte (por ejemplo, correo por SMS) sin reescribir todo, y dos personas no podrían trabajar a la vez sin pisarse. Se rompen SRP, OCP y DIP al mismo tiempo.

**2. ¿Qué riesgos existen si se agregan funcionalidades que el cliente no pidió?**

- Hay más código que mantener y más lugares donde pueden aparecer errores.
- Se pierde tiempo en cosas que no eran prioridad y se retrasa lo que sí se necesitaba.
- Puedes construir algo distinto a lo que el cliente realmente quería.
- Algunas funciones traen responsabilidades extra. Por ejemplo, un historial clínico exige proteger datos muy sensibles.
- Todo eso complica agregar después lo que el cliente sí pida.

**3. ¿Cómo afecta la repetición de código al mantenimiento del sistema?**

Si la misma regla está copiada en varios lugares, cada cambio hay que hacerlo en todos, y si se olvida uno, el sistema se comporta distinto según por dónde entres. Por ejemplo, si la revisión de disponibilidad estuviera copiada en agendar y en reprogramar, y solo arreglo una, alguien podría reservar doble por el camino que olvidé. Además, se gasta más tiempo corrigiendo y probando.

**4. ¿Por qué una solución simple puede ser mejor que una solución demasiado compleja?**

Porque es más fácil de entender, de probar y de cambiar. Una solución complicada cobra su costo desde el primer día, se use o no se use lo que se agregó. En cambio, si algo simple está bien separado, se puede mejorar cuando aparezca una necesidad real.

**5. ¿Qué ventaja tiene separar la lógica de citas, pacientes y médicos?**

- Los cambios quedan en un solo lugar: si cambio cómo se registra un paciente, las reglas de las citas no se afectan.
- Cada parte se puede probar por separado.
- Es más fácil encontrar dónde está un error.
- Varias personas pueden trabajar a la vez sin estorbarse.
- Cada parte se puede reutilizar o reemplazar sin tocar las demás.

**6. ¿Cómo se puede preparar el sistema para crecer sin hacerlo complicado desde el inicio?**

Dejando puntos de extensión solo donde ya se sabe que habrá cambios (en este caso los recordatorios, el guardado de datos y la hora), manteniendo las capas separadas, usando clases pequeñas con una sola responsabilidad y mejorando el código cuando aparezca una necesidad real, no antes. Así el sistema queda abierto donde es probable que cambie y simple en todo lo demás.

## Autor

Christian Crespo, estudiante del Instituto Tecnológico de Las Américas (ITLA).
