using Grpc.Core;
using PreProcessing;

namespace One_Health.PreProcessor.Services;

public class PreProcessingServiceImpl : PreProcessingService.PreProcessingServiceBase
{
    private readonly ILogger<PreProcessingServiceImpl> _logger;

    // Valid ranges per data type (min, max)
    private static readonly Dictionary<string, (double Min, double Max, string Unit)> DataRanges = new()
    {
        { "TEMP",         (-50,    60,     "°C")     },
        { "HUM",          (0,      100,    "%")      },
        { "PM2.5",        (0,      1000,   "µg/m³")  },
        { "PM10",         (0,      1000,   "µg/m³")  },
        { "RUIDO",        (0,      150,    "dB")     },
        { "AR",           (0,      500,    "AQI")    },
        { "LUMINOSIDADE", (0,      150000, "lux")    },
    };

    public PreProcessingServiceImpl(ILogger<PreProcessingServiceImpl> logger)
    {
        _logger = logger;
    }

    public override Task<ProcessedSensorData> PreProcess(RawSensorData request, ServerCallContext context)
    {
        _logger.LogInformation("PreProcess: Sensor={SensorId} Type={DataType} Value={Value}",
            request.SensorId, request.DataType, request.Value);

        var result = new ProcessedSensorData
        {
            SensorId  = request.SensorId,
            Zone      = request.Zone,
            DataType  = request.DataType,
            Timestamp = request.Timestamp,
        };

        // Normalize and validate
        double normalizedValue = NormalizeValue(request.DataType, request.Value);

        if (!DataRanges.TryGetValue(request.DataType, out var range))
        {
            // Unknown type — pass through
            result.NormalizedValue = normalizedValue;
            result.Unit            = "unknown";
            result.IsValid         = true;
        }
        else if (normalizedValue < range.Min || normalizedValue > range.Max)
        {
            result.NormalizedValue = normalizedValue;
            result.Unit            = range.Unit;
            result.IsValid         = false;
            result.ErrorMessage    = $"Valor {normalizedValue} fora do intervalo [{range.Min}, {range.Max}] para {request.DataType}";
            _logger.LogWarning("Validation failed: {Message}", result.ErrorMessage);
        }
        else
        {
            result.NormalizedValue = normalizedValue;
            result.Unit            = range.Unit;
            result.IsValid         = true;
        }

        return Task.FromResult(result);
    }

    private static double NormalizeValue(string dataType, double value)
    {
        // Example normalization: convert Fahrenheit to Celsius if value looks like °F
        // A TEMP value above 60 and below 160 is likely Fahrenheit
        if (dataType == "TEMP" && value > 60 && value < 160)
        {
            double celsius = (value - 32) * 5.0 / 9.0;
            return Math.Round(celsius, 2);
        }

        // HUM: clamp to 0-100 if slightly over due to sensor drift
        if (dataType == "HUM")
            return Math.Round(Math.Clamp(value, 0, 100), 2);

        return Math.Round(value, 4);
    }
}
