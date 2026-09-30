using SistemaCitasMedicas.Application.Extensiones;
using SistemaCitasMedicas.Application.Interfaces;
using SistemaCitasMedicas.Application.Servicios;
using SistemaCitasMedicas.Application.Validacion;
using SistemaCitasMedicas.Domain.Entidades;
using SistemaCitasMedicas.Domain.Excepciones;
using SistemaCitasMedicas.Infrastructure.Notificaciones;
using SistemaCitasMedicas.Infrastructure.Repositorios;
using SistemaCitasMedicas.Infrastructure.Tiempo;
using SistemaCitasMedicas.Presentation;
using System.Globalization;
using System.Net.Mail;
using static SistemaCitasMedicas.Presentation.EntradaConsola;

// =====================================================================================
// SISTEMA DE GESTIÓN DE CITAS MÉDICAS  -  Programación 2  -  ITLA
// Versión de UN SOLO ARCHIVO para pegar en Visual Studio:
//   1. Archivo > Nuevo > Proyecto > "Aplicación de consola" (C#, .NET 6 o superior)
//   2. Reemplaza TODO el contenido de Program.cs por este archivo
//   3. Ejecuta con Ctrl+F5
// Para entregar en GitHub se recomienda usar la versión por carpetas y archivos (SoC),
// que es exactamente este mismo código separado en sus archivos.
// =====================================================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Composition root: el ÚNICO lugar donde se eligen las implementaciones concretas (DIP). ──

// Infraestructura
IReloj reloj = new SistemaReloj();
var pacienteRepo = new RepositorioEnMemoria<Paciente>();
var medicoRepo = new RepositorioEnMemoria<Medico>();
var especialidadRepo = new RepositorioEnMemoria<Especialidad>();
var citaRepo = new CitaRepositorioEnMemoria();

// Canales de recordatorio. Para agregar SMS o WhatsApp solo se añade una línea aquí.
INotificador[] notificadores = { new EmailNotificador(Console.Out) };

// Validadores
IValidador<Paciente> validadorPaciente = new PersonaValidador<Paciente>();
IValidador<Medico> validadorMedico = new PersonaValidador<Medico>();
IValidador<Especialidad> validadorEspecialidad = new EspecialidadValidador();
IValidador<Cita> validadorCita = new CitaValidador(reloj);

// Servicios de aplicación
var disponibilidad = new VerificadorDisponibilidad(citaRepo);
var pacienteService = new PacienteService(pacienteRepo, validadorPaciente);
var especialidadService = new EspecialidadService(especialidadRepo, validadorEspecialidad);
var medicoService = new MedicoService(medicoRepo, especialidadRepo, validadorMedico);
var citaService = new CitaService(citaRepo, pacienteRepo, medicoRepo, validadorCita, disponibilidad);
var recordatorioService = new RecordatorioService(citaRepo, pacienteRepo, medicoRepo, notificadores);

// Presentación
new MenuConsola(pacienteService, medicoService, especialidadService, citaService, recordatorioService).Ejecutar();

// ───────── Domain/Excepciones/SistemaCitasException.cs ─────────
namespace SistemaCitasMedicas.Domain.Excepciones
{
    /// <summary>Base de todas las excepciones controladas del sistema.</summary>
    public abstract class SistemaCitasException : Exception
    {
        protected SistemaCitasException(string mensaje) : base(mensaje) { }
    }

    /// <summary>Los datos recibidos no son válidos (campo vacío, fecha pasada, etc.).</summary>
    public sealed class ValidacionException : SistemaCitasException
    {
        public ValidacionException(string mensaje) : base(mensaje) { }
    }

    /// <summary>La entidad solicitada no existe.</summary>
    public sealed class NoEncontradoException : SistemaCitasException
    {
        public NoEncontradoException(string mensaje) : base(mensaje) { }
    }

    /// <summary>Se viola una regla de negocio (médico ocupado, cita ya cancelada, etc.).</summary>
    public sealed class ReglaNegocioException : SistemaCitasException
    {
        public ReglaNegocioException(string mensaje) : base(mensaje) { }
    }
}

// ───────── Domain/Entidades/Cita.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    public class Cita : Entidad
    {
        /// <summary>Duración fija de cada cita (KISS: sin duraciones configurables por ahora).</summary>
        public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(30);

        public int PacienteId { get; set; }
        public int MedicoId { get; set; }
        public DateTime FechaHora { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public EstadoCita Estado { get; set; } = EstadoCita.Programada;

        public DateTime FechaFin => FechaHora + Duracion;
        public bool EstaActiva => Estado == EstadoCita.Programada;

        /// <summary>¿Esta cita ocupa el horario que empieza en <paramref name="inicio"/>?</summary>
        public bool SeSolapaCon(DateTime inicio)
            => EstaActiva && inicio < FechaFin && inicio + Duracion > FechaHora;

        public void AsegurarActiva()
        {
            if (!EstaActiva)
                throw new ReglaNegocioException($"La cita {Id} está cancelada.");
        }

        public void Cancelar()
        {
            AsegurarActiva();
            Estado = EstadoCita.Cancelada;
        }

        public void Reprogramar(DateTime nuevaFecha)
        {
            AsegurarActiva();
            FechaHora = nuevaFecha;
        }
    }
}

// ───────── Domain/Entidades/Entidad.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    /// <summary>Base común: toda entidad tiene un identificador.</summary>
    public abstract class Entidad
    {
        public int Id { get; set; }
    }
}

// ───────── Domain/Entidades/Especialidad.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    public class Especialidad : Entidad
    {
        public string Nombre { get; set; } = string.Empty;
    }
}

// ───────── Domain/Entidades/EstadoCita.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    public enum EstadoCita
    {
        Programada,
        Cancelada
    }
}

// ───────── Domain/Entidades/Medico.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    public class Medico : Persona
    {
        /// <summary>Especialidad asignada; null mientras no se le asigne una.</summary>
        public int? EspecialidadId { get; set; }
    }
}

// ───────── Domain/Entidades/Paciente.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    public class Paciente : Persona
    {
    }
}

// ───────── Domain/Entidades/Persona.cs ─────────
namespace SistemaCitasMedicas.Domain.Entidades
{
    /// <summary>
    /// Datos comunes de una persona. Paciente y Medico la heredan (DRY):
    /// comparten atributos y la misma validación.
    /// </summary>
    public abstract class Persona : Entidad
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string NombreCompleto => $"{Nombre} {Apellido}";
    }
}

// ───────── Application/Interfaces/ICitaRepositorio.cs ─────────
namespace SistemaCitasMedicas.Application.Interfaces
{
    /// <summary>Solo lo que las citas necesitan además del contrato genérico.</summary>
    public interface ICitaRepositorio : IRepositorio<Cita>
    {
        IReadOnlyList<Cita> ObtenerPorPaciente(int pacienteId);
        IReadOnlyList<Cita> ObtenerPorMedico(int medicoId);
    }
}

// ───────── Application/Interfaces/INotificador.cs ─────────
namespace SistemaCitasMedicas.Application.Interfaces
{
    /// <summary>
    /// Un canal de recordatorio (correo, SMS, WhatsApp...). Para agregar un canal nuevo
    /// basta con crear otra clase que implemente esta interfaz (OCP).
    /// </summary>
    public interface INotificador
    {
        string Canal { get; }
        void Enviar(Paciente paciente, string mensaje);
    }
}

// ───────── Application/Interfaces/IReloj.cs ─────────
namespace SistemaCitasMedicas.Application.Interfaces
{
    /// <summary>Abstrae la hora actual para poder validar fechas y probar sin depender del reloj real.</summary>
    public interface IReloj
    {
        DateTime Ahora { get; }
    }
}

// ───────── Application/Interfaces/IRepositorio.cs ─────────
namespace SistemaCitasMedicas.Application.Interfaces
{
    /// <summary>Contrato genérico y pequeño de persistencia (ISP + DRY).</summary>
    public interface IRepositorio<T> where T : Entidad
    {
        T Agregar(T entidad);
        T? ObtenerPorId(int id);
        IReadOnlyList<T> ObtenerTodos();
        void Actualizar(T entidad);
    }
}

// ───────── Application/Interfaces/IVerificadorDisponibilidad.cs ─────────
namespace SistemaCitasMedicas.Application.Interfaces
{
    /// <summary>Única regla de disponibilidad del sistema.</summary>
    public interface IVerificadorDisponibilidad
    {
        /// <summary>Lanza ReglaNegocioException si el médico ya tiene una cita activa en ese horario.</summary>
        void Asegurar(int medicoId, DateTime fechaHora, int? excluirCitaId = null);
    }
}

// ───────── Application/Validacion/CitaValidador.cs ─────────
namespace SistemaCitasMedicas.Application.Validacion
{
    public class CitaValidador : IValidador<Cita>
    {
        private readonly IReloj _reloj;

        public CitaValidador(IReloj reloj) => _reloj = reloj;

        public void Validar(Cita cita)
        {
            Reglas.IdPositivo(cita.PacienteId, "paciente");
            Reglas.IdPositivo(cita.MedicoId, "médico");
            Reglas.FechaFutura(cita.FechaHora, _reloj.Ahora, "fecha de la cita");
        }
    }
}

// ───────── Application/Validacion/EspecialidadValidador.cs ─────────
namespace SistemaCitasMedicas.Application.Validacion
{
    public class EspecialidadValidador : IValidador<Especialidad>
    {
        public void Validar(Especialidad especialidad)
            => Reglas.NoVacio(especialidad.Nombre, "nombre de la especialidad");
    }
}

// ───────── Application/Validacion/IValidador.cs ─────────
namespace SistemaCitasMedicas.Application.Validacion
{
    public interface IValidador<in T>
    {
        /// <summary>Lanza ValidacionException si el objeto no es válido.</summary>
        void Validar(T objeto);
    }
}

// ───────── Application/Validacion/PersonaValidador.cs ─────────
namespace SistemaCitasMedicas.Application.Validacion
{
    /// <summary>Un solo validador para pacientes y médicos, porque ambos son Persona (DRY + LSP).</summary>
    public class PersonaValidador<T> : IValidador<T> where T : Persona
    {
        public void Validar(T persona)
        {
            Reglas.NoVacio(persona.Nombre, "nombre");
            Reglas.NoVacio(persona.Apellido, "apellido");
            Reglas.TelefonoValido(persona.Telefono);
            Reglas.EmailValido(persona.Email);
        }
    }
}

// ───────── Application/Validacion/Reglas.cs ─────────
namespace SistemaCitasMedicas.Application.Validacion
{
    /// <summary>
    /// Reglas de validación reutilizables. Es el ÚNICO lugar donde vive la lógica de
    /// "campo vacío", "fecha válida", "email válido", etc. (DRY).
    /// </summary>
    public static class Reglas
    {
        public static void NoVacio(string? valor, string campo)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new ValidacionException($"El campo '{campo}' es obligatorio.");
        }

        public static void IdPositivo(int id, string campo)
        {
            if (id <= 0)
                throw new ValidacionException($"El campo '{campo}' debe ser un identificador válido.");
        }

        public static void FechaFutura(DateTime fecha, DateTime ahora, string campo)
        {
            if (fecha <= ahora)
                throw new ValidacionException($"El campo '{campo}' debe ser una fecha y hora futura.");
        }

        public static void EmailValido(string? email)
        {
            NoVacio(email, "email");
            var valido = MailAddress.TryCreate(email, out var direccion) && direccion.Address == email!.Trim();
            if (!valido)
                throw new ValidacionException("El email no tiene un formato válido.");
        }

        public static void TelefonoValido(string? telefono)
        {
            NoVacio(telefono, "teléfono");
            if (telefono!.Count(char.IsDigit) < 7)
                throw new ValidacionException("El teléfono debe tener al menos 7 dígitos.");
        }
    }
}

// ───────── Application/Extensiones/RepositorioExtensiones.cs ─────────
namespace SistemaCitasMedicas.Application.Extensiones
{
    public static class RepositorioExtensiones
    {
        /// <summary>Busca por id o lanza NoEncontradoException. Evita repetir este chequeo en cada servicio (DRY).</summary>
        public static T ObtenerObligatorio<T>(this IRepositorio<T> repositorio, int id) where T : Entidad
            => repositorio.ObtenerPorId(id)
               ?? throw new NoEncontradoException($"No existe {typeof(T).Name} con id {id}.");
    }
}

// ───────── Application/Servicios/CitaService.cs ─────────
namespace SistemaCitasMedicas.Application.Servicios
{
    /// <summary>Responsabilidad única: casos de uso de citas (agendar, consultar, cancelar, reprogramar).</summary>
    public class CitaService
    {
        private readonly ICitaRepositorio _citas;
        private readonly IRepositorio<Paciente> _pacientes;
        private readonly IRepositorio<Medico> _medicos;
        private readonly IValidador<Cita> _validador;
        private readonly IVerificadorDisponibilidad _disponibilidad;

        public CitaService(
            ICitaRepositorio citas,
            IRepositorio<Paciente> pacientes,
            IRepositorio<Medico> medicos,
            IValidador<Cita> validador,
            IVerificadorDisponibilidad disponibilidad)
        {
            _citas = citas;
            _pacientes = pacientes;
            _medicos = medicos;
            _validador = validador;
            _disponibilidad = disponibilidad;
        }

        public Cita Agendar(int pacienteId, int medicoId, DateTime fechaHora, string motivo)
        {
            var cita = new Cita
            {
                PacienteId = pacienteId,
                MedicoId = medicoId,
                FechaHora = fechaHora,
                Motivo = motivo.Trim()
            };

            _validador.Validar(cita);
            _pacientes.ObtenerObligatorio(pacienteId);
            _medicos.ObtenerObligatorio(medicoId);
            _disponibilidad.Asegurar(medicoId, fechaHora);

            return _citas.Agregar(cita);
        }

        public Cita Cancelar(int citaId)
        {
            var cita = _citas.ObtenerObligatorio(citaId);
            cita.Cancelar();
            _citas.Actualizar(cita);
            return cita;
        }

        public Cita Reprogramar(int citaId, DateTime nuevaFecha)
        {
            var cita = _citas.ObtenerObligatorio(citaId);
            cita.AsegurarActiva();

            // Se reutilizan el mismo validador y la misma regla de disponibilidad que al agendar.
            _validador.Validar(new Cita { PacienteId = cita.PacienteId, MedicoId = cita.MedicoId, FechaHora = nuevaFecha });
            _disponibilidad.Asegurar(cita.MedicoId, nuevaFecha, excluirCitaId: cita.Id);

            cita.Reprogramar(nuevaFecha);
            _citas.Actualizar(cita);
            return cita;
        }

        public Cita ObtenerPorId(int id) => _citas.ObtenerObligatorio(id);

        public IReadOnlyList<Cita> ConsultarPorPaciente(int pacienteId)
        {
            _pacientes.ObtenerObligatorio(pacienteId);
            return _citas.ObtenerPorPaciente(pacienteId);
        }

        public IReadOnlyList<Cita> ConsultarPorMedico(int medicoId)
        {
            _medicos.ObtenerObligatorio(medicoId);
            return _citas.ObtenerPorMedico(medicoId);
        }
    }
}

// ───────── Application/Servicios/EspecialidadService.cs ─────────
namespace SistemaCitasMedicas.Application.Servicios
{
    /// <summary>Responsabilidad única: casos de uso de especialidades médicas.</summary>
    public class EspecialidadService
    {
        private readonly IRepositorio<Especialidad> _especialidades;
        private readonly IValidador<Especialidad> _validador;

        public EspecialidadService(IRepositorio<Especialidad> especialidades, IValidador<Especialidad> validador)
        {
            _especialidades = especialidades;
            _validador = validador;
        }

        public Especialidad Registrar(string nombre)
        {
            var especialidad = new Especialidad { Nombre = nombre.Trim() };
            _validador.Validar(especialidad);

            var existe = _especialidades.ObtenerTodos()
                .Any(e => string.Equals(e.Nombre, especialidad.Nombre, StringComparison.OrdinalIgnoreCase));
            if (existe)
                throw new ReglaNegocioException($"La especialidad '{especialidad.Nombre}' ya está registrada.");

            return _especialidades.Agregar(especialidad);
        }

        public Especialidad ObtenerPorId(int id) => _especialidades.ObtenerObligatorio(id);

        public IReadOnlyList<Especialidad> Listar() => _especialidades.ObtenerTodos();
    }
}

// ───────── Application/Servicios/MedicoService.cs ─────────
namespace SistemaCitasMedicas.Application.Servicios
{
    /// <summary>Responsabilidad única: casos de uso de médicos y su asignación a especialidades.</summary>
    public class MedicoService
    {
        private readonly IRepositorio<Medico> _medicos;
        private readonly IRepositorio<Especialidad> _especialidades;
        private readonly IValidador<Medico> _validador;

        public MedicoService(
            IRepositorio<Medico> medicos,
            IRepositorio<Especialidad> especialidades,
            IValidador<Medico> validador)
        {
            _medicos = medicos;
            _especialidades = especialidades;
            _validador = validador;
        }

        public Medico Registrar(string nombre, string apellido, string telefono, string email)
        {
            var medico = new Medico
            {
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Telefono = telefono.Trim(),
                Email = email.Trim()
            };
            _validador.Validar(medico);
            return _medicos.Agregar(medico);
        }

        public Medico AsignarEspecialidad(int medicoId, int especialidadId)
        {
            var medico = _medicos.ObtenerObligatorio(medicoId);
            var especialidad = _especialidades.ObtenerObligatorio(especialidadId);

            medico.EspecialidadId = especialidad.Id;
            _medicos.Actualizar(medico);
            return medico;
        }

        public Medico ObtenerPorId(int id) => _medicos.ObtenerObligatorio(id);

        public IReadOnlyList<Medico> Listar() => _medicos.ObtenerTodos();

        public IReadOnlyList<Medico> ListarPorEspecialidad(int especialidadId)
            => _medicos.ObtenerTodos().Where(m => m.EspecialidadId == especialidadId).ToList();
    }
}

// ───────── Application/Servicios/PacienteService.cs ─────────
namespace SistemaCitasMedicas.Application.Servicios
{
    /// <summary>Responsabilidad única: casos de uso de pacientes.</summary>
    public class PacienteService
    {
        private readonly IRepositorio<Paciente> _pacientes;
        private readonly IValidador<Paciente> _validador;

        public PacienteService(IRepositorio<Paciente> pacientes, IValidador<Paciente> validador)
        {
            _pacientes = pacientes;
            _validador = validador;
        }

        public Paciente Registrar(string nombre, string apellido, string telefono, string email)
        {
            var paciente = new Paciente
            {
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Telefono = telefono.Trim(),
                Email = email.Trim()
            };
            _validador.Validar(paciente);
            return _pacientes.Agregar(paciente);
        }

        public Paciente ObtenerPorId(int id) => _pacientes.ObtenerObligatorio(id);

        public IReadOnlyList<Paciente> Listar() => _pacientes.ObtenerTodos();
    }
}

// ───────── Application/Servicios/RecordatorioService.cs ─────────
namespace SistemaCitasMedicas.Application.Servicios
{
    /// <summary>
    /// Responsabilidad única: armar y enviar el recordatorio de una cita.
    /// No conoce ningún canal concreto: recibe todos los INotificador registrados (DIP + OCP).
    /// </summary>
    public class RecordatorioService
    {
        private readonly IRepositorio<Cita> _citas;
        private readonly IRepositorio<Paciente> _pacientes;
        private readonly IRepositorio<Medico> _medicos;
        private readonly IEnumerable<INotificador> _notificadores;

        public RecordatorioService(
            IRepositorio<Cita> citas,
            IRepositorio<Paciente> pacientes,
            IRepositorio<Medico> medicos,
            IEnumerable<INotificador> notificadores)
        {
            _citas = citas;
            _pacientes = pacientes;
            _medicos = medicos;
            _notificadores = notificadores;
        }

        /// <summary>Envía el recordatorio por todos los canales disponibles y devuelve los canales usados.</summary>
        public IReadOnlyList<string> Enviar(int citaId)
        {
            var cita = _citas.ObtenerObligatorio(citaId);
            cita.AsegurarActiva();

            var paciente = _pacientes.ObtenerObligatorio(cita.PacienteId);
            var medico = _medicos.ObtenerObligatorio(cita.MedicoId);

            var mensaje = $"Hola {paciente.Nombre}, le recordamos su cita con {medico.NombreCompleto} " +
                          $"el {cita.FechaHora:yyyy-MM-dd} a las {cita.FechaHora:HH:mm}.";

            var canales = new List<string>();
            foreach (var notificador in _notificadores)
            {
                notificador.Enviar(paciente, mensaje);
                canales.Add(notificador.Canal);
            }
            return canales;
        }
    }
}

// ───────── Application/Servicios/VerificadorDisponibilidad.cs ─────────
namespace SistemaCitasMedicas.Application.Servicios
{
    /// <summary>
    /// Responsabilidad única: decidir si un médico está libre. Agendar y reprogramar
    /// usan esta misma clase, así la regla no se repite (DRY).
    /// </summary>
    public class VerificadorDisponibilidad : IVerificadorDisponibilidad
    {
        private readonly ICitaRepositorio _citas;

        public VerificadorDisponibilidad(ICitaRepositorio citas) => _citas = citas;

        public void Asegurar(int medicoId, DateTime fechaHora, int? excluirCitaId = null)
        {
            var ocupado = _citas.ObtenerPorMedico(medicoId)
                .Any(c => c.Id != excluirCitaId && c.SeSolapaCon(fechaHora));

            if (ocupado)
                throw new ReglaNegocioException(
                    $"El médico {medicoId} ya tiene una cita en el horario {fechaHora:yyyy-MM-dd HH:mm}.");
        }
    }
}

// ───────── Infrastructure/Repositorios/CitaRepositorioEnMemoria.cs ─────────
namespace SistemaCitasMedicas.Infrastructure.Repositorios
{
    public class CitaRepositorioEnMemoria : RepositorioEnMemoria<Cita>, ICitaRepositorio
    {
        public IReadOnlyList<Cita> ObtenerPorPaciente(int pacienteId)
            => ObtenerTodos().Where(c => c.PacienteId == pacienteId).OrderBy(c => c.FechaHora).ToList();

        public IReadOnlyList<Cita> ObtenerPorMedico(int medicoId)
            => ObtenerTodos().Where(c => c.MedicoId == medicoId).OrderBy(c => c.FechaHora).ToList();
    }
}

// ───────── Infrastructure/Repositorios/RepositorioEnMemoria.cs ─────────
namespace SistemaCitasMedicas.Infrastructure.Repositorios
{
    /// <summary>
    /// Un solo repositorio genérico sirve para todas las entidades (DRY).
    /// Guarda en memoria: suficiente para la primera versión (KISS/YAGNI). Más adelante
    /// se puede reemplazar por una base de datos sin tocar la capa de aplicación.
    /// </summary>
    public class RepositorioEnMemoria<T> : IRepositorio<T> where T : Entidad
    {
        private readonly Dictionary<int, T> _datos = new();
        private int _siguienteId = 1;

        public T Agregar(T entidad)
        {
            entidad.Id = _siguienteId++;
            _datos[entidad.Id] = entidad;
            return entidad;
        }

        public T? ObtenerPorId(int id) => _datos.GetValueOrDefault(id);

        public IReadOnlyList<T> ObtenerTodos() => _datos.Values.OrderBy(e => e.Id).ToList();

        public void Actualizar(T entidad)
        {
            if (!_datos.ContainsKey(entidad.Id))
                throw new NoEncontradoException($"No existe {typeof(T).Name} con id {entidad.Id}.");
            _datos[entidad.Id] = entidad;
        }
    }
}

// ───────── Infrastructure/Notificaciones/EmailNotificador.cs ─────────
namespace SistemaCitasMedicas.Infrastructure.Notificaciones
{
    /// <summary>
    /// Canal de correo electrónico. En esta versión el envío se simula escribiendo en la salida
    /// (YAGNI: aún no se necesita un servidor SMTP real).
    /// Para SMS/WhatsApp/push se crearía otra clase INotificador, sin modificar esta.
    /// </summary>
    public class EmailNotificador : INotificador
    {
        private readonly TextWriter _salida;

        public EmailNotificador(TextWriter salida) => _salida = salida;

        public string Canal => "Email";

        public void Enviar(Paciente paciente, string mensaje)
            => _salida.WriteLine($"[EMAIL] Para: {paciente.Email} | {mensaje}");
    }
}

// ───────── Infrastructure/Tiempo/SistemaReloj.cs ─────────
namespace SistemaCitasMedicas.Infrastructure.Tiempo
{
    public class SistemaReloj : IReloj
    {
        public DateTime Ahora => DateTime.Now;
    }
}

// ───────── Presentation/EntradaConsola.cs ─────────
namespace SistemaCitasMedicas.Presentation
{
    /// <summary>Lectura de datos por consola. Solo se ocupa de pedir y convertir texto, no de validar reglas de negocio.</summary>
    public static class EntradaConsola
    {
        public const string FormatoFecha = "yyyy-MM-dd HH:mm";

        public static string LeerTexto(string etiqueta)
        {
            Console.Write($"{etiqueta}: ");
            return Console.ReadLine() ?? string.Empty;
        }

        public static int LeerEntero(string etiqueta)
        {
            Console.Write($"{etiqueta}: ");
            return int.TryParse(Console.ReadLine(), out var valor) ? valor : 0;
        }

        public static DateTime LeerFecha(string etiqueta)
        {
            Console.Write($"{etiqueta} ({FormatoFecha}): ");
            var texto = Console.ReadLine();
            return DateTime.TryParseExact(texto, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                ? fecha
                : DateTime.MinValue; // Fecha inválida: el validador la rechaza como "no futura".
        }
    }
}

// ───────── Presentation/MenuConsola.cs ─────────
namespace SistemaCitasMedicas.Presentation
{
    /// <summary>Interfaz de usuario por consola. Solo orquesta: cada acción delega en un servicio.</summary>
    public class MenuConsola
    {
        private readonly PacienteService _pacientes;
        private readonly MedicoService _medicos;
        private readonly EspecialidadService _especialidades;
        private readonly CitaService _citas;
        private readonly RecordatorioService _recordatorios;

        public MenuConsola(
            PacienteService pacientes,
            MedicoService medicos,
            EspecialidadService especialidades,
            CitaService citas,
            RecordatorioService recordatorios)
        {
            _pacientes = pacientes;
            _medicos = medicos;
            _especialidades = especialidades;
            _citas = citas;
            _recordatorios = recordatorios;
        }

        public void Ejecutar()
        {
            while (true)
            {
                MostrarMenu();
                var opcion = LeerTexto("Opción");
                if (opcion == "0") return;

                try
                {
                    EjecutarOpcion(opcion);
                }
                catch (SistemaCitasException ex)
                {
                    Console.WriteLine($"  ✗ {ex.Message}");
                }
            }
        }

        private static void MostrarMenu()
        {
            Console.WriteLine();
            Console.WriteLine("=== Sistema de Gestión de Citas Médicas ===");
            Console.WriteLine(" 1. Registrar paciente");
            Console.WriteLine(" 2. Registrar médico");
            Console.WriteLine(" 3. Registrar especialidad");
            Console.WriteLine(" 4. Asignar médico a especialidad");
            Console.WriteLine(" 5. Agendar cita");
            Console.WriteLine(" 6. Consultar citas por paciente");
            Console.WriteLine(" 7. Consultar citas por médico");
            Console.WriteLine(" 8. Cancelar cita");
            Console.WriteLine(" 9. Reprogramar cita");
            Console.WriteLine("10. Enviar recordatorio de cita");
            Console.WriteLine("11. Ver listados (pacientes, médicos, especialidades)");
            Console.WriteLine(" 0. Salir");
        }

        private void EjecutarOpcion(string opcion)
        {
            switch (opcion)
            {
                case "1": RegistrarPaciente(); break;
                case "2": RegistrarMedico(); break;
                case "3": RegistrarEspecialidad(); break;
                case "4": AsignarEspecialidad(); break;
                case "5": AgendarCita(); break;
                case "6": MostrarCitas(_citas.ConsultarPorPaciente(LeerEntero("Id del paciente"))); break;
                case "7": MostrarCitas(_citas.ConsultarPorMedico(LeerEntero("Id del médico"))); break;
                case "8": CancelarCita(); break;
                case "9": ReprogramarCita(); break;
                case "10": EnviarRecordatorio(); break;
                case "11": MostrarListados(); break;
                default: Console.WriteLine("  Opción no válida."); break;
            }
        }

        private void RegistrarPaciente()
        {
            var p = _pacientes.Registrar(
                LeerTexto("Nombre"), LeerTexto("Apellido"), LeerTexto("Teléfono"), LeerTexto("Email"));
            Console.WriteLine($"  ✓ Paciente registrado con id {p.Id}.");
        }

        private void RegistrarMedico()
        {
            var m = _medicos.Registrar(
                LeerTexto("Nombre"), LeerTexto("Apellido"), LeerTexto("Teléfono"), LeerTexto("Email"));
            Console.WriteLine($"  ✓ Médico registrado con id {m.Id}.");
        }

        private void RegistrarEspecialidad()
        {
            var e = _especialidades.Registrar(LeerTexto("Nombre de la especialidad"));
            Console.WriteLine($"  ✓ Especialidad registrada con id {e.Id}.");
        }

        private void AsignarEspecialidad()
        {
            var m = _medicos.AsignarEspecialidad(LeerEntero("Id del médico"), LeerEntero("Id de la especialidad"));
            Console.WriteLine($"  ✓ {m.NombreCompleto} asignado a la especialidad {m.EspecialidadId}.");
        }

        private void AgendarCita()
        {
            var c = _citas.Agendar(
                LeerEntero("Id del paciente"), LeerEntero("Id del médico"),
                LeerFecha("Fecha y hora"), LeerTexto("Motivo (opcional)"));
            Console.WriteLine($"  ✓ Cita agendada con id {c.Id}.");
        }

        private void CancelarCita()
        {
            var c = _citas.Cancelar(LeerEntero("Id de la cita"));
            Console.WriteLine($"  ✓ Cita {c.Id} cancelada.");
        }

        private void ReprogramarCita()
        {
            var c = _citas.Reprogramar(LeerEntero("Id de la cita"), LeerFecha("Nueva fecha y hora"));
            Console.WriteLine($"  ✓ Cita {c.Id} reprogramada para {c.FechaHora:yyyy-MM-dd HH:mm}.");
        }

        private void EnviarRecordatorio()
        {
            var canales = _recordatorios.Enviar(LeerEntero("Id de la cita"));
            Console.WriteLine($"  ✓ Recordatorio enviado por: {string.Join(", ", canales)}.");
        }

        private static void MostrarCitas(IReadOnlyList<Cita> citas)
        {
            if (citas.Count == 0) { Console.WriteLine("  (sin citas)"); return; }
            foreach (var c in citas)
                Console.WriteLine($"  Cita {c.Id} | {c.FechaHora:yyyy-MM-dd HH:mm} | Paciente {c.PacienteId} | " +
                                  $"Médico {c.MedicoId} | {c.Estado} | {c.Motivo}");
        }

        private void MostrarListados()
        {
            Console.WriteLine("  Pacientes:");
            foreach (var p in _pacientes.Listar()) Console.WriteLine($"    {p.Id}. {p.NombreCompleto} ({p.Email})");
            Console.WriteLine("  Médicos:");
            foreach (var m in _medicos.Listar())
                Console.WriteLine($"    {m.Id}. {m.NombreCompleto} (especialidad: {m.EspecialidadId?.ToString() ?? "sin asignar"})");
            Console.WriteLine("  Especialidades:");
            foreach (var e in _especialidades.Listar()) Console.WriteLine($"    {e.Id}. {e.Nombre}");
        }
    }
}