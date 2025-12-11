using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Diagnostics.Metrics;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// Helper class for testing metrics by listening to meter measurements
/// </summary>
public class MetricsTestHelper : IDisposable
{
    private readonly MeterListener _meterListener;
    private readonly List<Measurement<long>> _longMeasurements = new();
    private readonly List<Measurement<double>> _doubleMeasurements = new();
    private readonly List<Measurement<int>> _intMeasurements = new();

    public MetricsTestHelper(string meterName)
    {
        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            lock (_longMeasurements)
            {
                _longMeasurements.Add(new Measurement<long>(measurement, tags));
            }
        });

        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            lock (_doubleMeasurements)
            {
                _doubleMeasurements.Add(new Measurement<double>(measurement, tags));
            }
        });

        _meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            lock (_intMeasurements)
            {
                _intMeasurements.Add(new Measurement<int>(measurement, tags));
            }
        });

        _meterListener.Start();
    }

    public List<Measurement<long>> GetLongMeasurements()
    {
        lock (_longMeasurements)
        {
            return new List<Measurement<long>>(_longMeasurements);
        }
    }

    public List<Measurement<double>> GetDoubleMeasurements()
    {
        lock (_doubleMeasurements)
        {
            return new List<Measurement<double>>(_doubleMeasurements);
        }
    }

    public List<Measurement<int>> GetIntMeasurements()
    {
        lock (_intMeasurements)
        {
            return new List<Measurement<int>>(_intMeasurements);
        }
    }

    public void Dispose()
    {
        _meterListener?.Dispose();
    }
}
