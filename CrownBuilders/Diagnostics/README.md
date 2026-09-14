# Watchdog de Diagnóstico para Crown Builders

El Watchdog es un agente de diagnóstico integrado que monitorea la aplicación MAUI Blazor Hybrid en tiempo real para detectar anomalías y proporcionar diagnóstico automático.

## Funcionalidades

- **Detección de excepciones no controladas**: Captura excepciones en el hilo principal y tareas en background
- **Detección de congelamientos de UI**: Monitorea el heartbeat del hilo principal para detectar freezes
- **Control de memoria**: Alerta cuando el consumo de memoria excede el umbral configurado
- **Captura de screenshots**: Toma capturas de pantalla cuando se detecta una anomalía
- **Buffer de logs circular**: Captura los últimos 200 líneas de Debug/Trace
- **Extracción de código fuente**: Intenta extraer el código del archivo/línea que falló
- **Diagnóstico con IA (opcional)**: Envía el reporte a OpenAI GPT-4o para análisis
- **Notificación a Visual Studio**: Escribe diagnóstico en la ventana Output y pausa el debugger si está conectado
- **Reportes locales**: Guarda reportes de crash en `FileSystem.AppDataDirectory/watchdog_reports/`

## Configuración

El Watchdog está configurado en `MauiProgram.cs`:

```csharp
WatchdogAgent.Initialize(new WatchdogOptions
{
    OpenAiApiKey = "", // Configurar con API key de OpenAI si se desea diagnóstico IA
    ProjectRootPath = @"C:\Users\david\source\repos\CrownBuilders",
    MemoryThresholdMb = 2048,
    FreezeThresholdMs = 3000
});
```

### Opciones de configuración

- `OpenAiApiKey`: API key de OpenAI para diagnóstico con GPT-4o (dejar vacío para desactivar)
- `ProjectRootPath`: Ruta raíz del proyecto para extracción de código fuente
- `MemoryThresholdMb`: Umbral de memoria en MB (default: 2048)
- `FreezeThresholdMs`: Umbral de tiempo sin heartbeat en ms (default: 3000)

## Heartbeat

El heartbeat se configura en `MainPage.xaml.cs` con un timer que llama a `WatchdogAgent.Heartbeat()` cada 500ms para monitorear que el hilo principal está vivo.

## Uso durante desarrollo

Cuando depuras la aplicación en Visual Studio:

1. Cualquier anomalía se reportará en la ventana "Output" con el prefijo `=== WATCHDOG: ANOMALÍA DETECTADA ===`
2. Si el debugger está conectado, `Debugger.Break()` pausará la ejecución automáticamente
3. Los reportes locales se guardan en la carpeta de datos de la aplicación

## Habilitar diagnóstico con IA

Para habilitar el diagnóstico automático con GPT-4o:

1. Obtén una API key de OpenAI
2. Configura `OpenAiApiKey` en `MauiProgram.cs`
3. El Watchdog enviará el paquete de diagnóstico (screenshot, logs, código fuente, stack trace) a GPT-4o
4. La respuesta aparecerá en la ventana Output con el prefijo `=== DIAGNÓSTICO IA (GPT-4o) ===`

## Archivos creados

- `Diagnostics/WatchdogAgent.cs`: Agente principal de diagnóstico
- `Diagnostics/WatchdogOptions.cs`: Clase de opciones de configuración
- `Diagnostics/README.md`: Esta documentación

## Modificaciones al proyecto

- `MauiProgram.cs`: Inicialización del Watchdog en `CreateMauiApp()`
- `MainPage.xaml.cs`: Timer de heartbeat para monitoreo de UI

## Notas

- El Watchdog usa un buffer circular de 200 líneas para logs, no depende de archivos externos
- La captura de screenshots depende del soporte de la plataforma (`Screenshot.Default.IsCaptureSupported`)
- La extracción de código fuente funciona solo en builds Debug con rutas de archivo disponibles
- El diagnóstico con IA requiere conexión a internet y una API key válida de OpenAI
