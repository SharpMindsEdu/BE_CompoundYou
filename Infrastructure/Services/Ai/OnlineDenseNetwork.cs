namespace Infrastructure.Services.Ai;

internal sealed class OnlineDenseNetwork
{
    private readonly int _inputSize;
    private readonly int _hiddenSize;
    private readonly double _learningRate;
    private readonly double _l2Regularization;
    private readonly double[][] _inputToHidden;
    private readonly double[] _hiddenBias;
    private readonly double[] _hiddenToOutput;
    private double _outputBias;

    public int InputSize => _inputSize;

    public int HiddenSize => _hiddenSize;

    public OnlineDenseNetwork(
        int inputSize,
        int hiddenSize,
        double learningRate,
        double l2Regularization,
        int seed
    )
    {
        _inputSize = Math.Max(1, inputSize);
        _hiddenSize = Math.Max(2, hiddenSize);
        _learningRate = Math.Clamp(learningRate, 0.00001d, 1d);
        _l2Regularization = Math.Max(0d, l2Regularization);

        _inputToHidden = new double[_hiddenSize][];
        _hiddenBias = new double[_hiddenSize];
        _hiddenToOutput = new double[_hiddenSize];

        var random = new Random(seed);
        var inputScale = Math.Sqrt(2d / _inputSize);
        var hiddenScale = Math.Sqrt(2d / _hiddenSize);

        for (var h = 0; h < _hiddenSize; h++)
        {
            _inputToHidden[h] = new double[_inputSize];
            for (var i = 0; i < _inputSize; i++)
            {
                _inputToHidden[h][i] = (random.NextDouble() * 2d - 1d) * inputScale;
            }

            _hiddenToOutput[h] = (random.NextDouble() * 2d - 1d) * hiddenScale;
            _hiddenBias[h] = 0d;
        }

        _outputBias = 0d;
    }

    public double Predict(ReadOnlySpan<double> features)
    {
        ValidateFeatures(features);

        var hiddenPreActivation = new double[_hiddenSize];
        var hiddenActivation = new double[_hiddenSize];
        Forward(features, hiddenPreActivation, hiddenActivation, out var outputPreActivation);
        return Math.Tanh(outputPreActivation);
    }

    public void Train(ReadOnlySpan<double> features, double target, double sampleWeight = 1d)
    {
        if (sampleWeight <= 0d)
        {
            return;
        }

        ValidateFeatures(features);
        var safeTarget = Math.Clamp(target, -1d, 1d);
        var weight = Math.Clamp(sampleWeight, 0.05d, 8d);

        var hiddenPreActivation = new double[_hiddenSize];
        var hiddenActivation = new double[_hiddenSize];
        Forward(features, hiddenPreActivation, hiddenActivation, out var outputPreActivation);

        var output = Math.Tanh(outputPreActivation);
        var outputDelta = (output - safeTarget) * (1d - output * output) * weight;

        _outputBias -= _learningRate * outputDelta;

        for (var h = 0; h < _hiddenSize; h++)
        {
            var w2BeforeUpdate = _hiddenToOutput[h];
            var gradW2 = outputDelta * hiddenActivation[h] + _l2Regularization * w2BeforeUpdate;
            _hiddenToOutput[h] -= _learningRate * gradW2;

            var reluDerivative = hiddenPreActivation[h] > 0d ? 1d : 0d;
            if (reluDerivative == 0d)
            {
                continue;
            }

            var hiddenDelta = outputDelta * w2BeforeUpdate * reluDerivative;
            _hiddenBias[h] -= _learningRate * hiddenDelta;

            var row = _inputToHidden[h];
            for (var i = 0; i < _inputSize; i++)
            {
                var weightBeforeUpdate = row[i];
                var gradient = hiddenDelta * features[i] + _l2Regularization * weightBeforeUpdate;
                row[i] -= _learningRate * gradient;
            }
        }
    }

    public OnlineDenseNetworkSnapshot ToSnapshot()
    {
        var inputToHidden = new double[_hiddenSize][];
        for (var h = 0; h < _hiddenSize; h++)
        {
            inputToHidden[h] = (double[])_inputToHidden[h].Clone();
        }

        return new OnlineDenseNetworkSnapshot(
            _inputSize,
            _hiddenSize,
            _learningRate,
            _l2Regularization,
            inputToHidden,
            (double[])_hiddenBias.Clone(),
            (double[])_hiddenToOutput.Clone(),
            _outputBias
        );
    }

    public bool TryLoadSnapshot(OnlineDenseNetworkSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return false;
        }

        if (
            snapshot.InputSize != _inputSize
            || snapshot.HiddenSize != _hiddenSize
            || snapshot.InputToHidden.Length != _hiddenSize
            || snapshot.HiddenBias.Length != _hiddenSize
            || snapshot.HiddenToOutput.Length != _hiddenSize
        )
        {
            return false;
        }

        for (var h = 0; h < _hiddenSize; h++)
        {
            var sourceRow = snapshot.InputToHidden[h];
            if (sourceRow.Length != _inputSize)
            {
                return false;
            }

            var targetRow = _inputToHidden[h];
            Array.Copy(sourceRow, targetRow, _inputSize);
            _hiddenBias[h] = snapshot.HiddenBias[h];
            _hiddenToOutput[h] = snapshot.HiddenToOutput[h];
        }

        _outputBias = snapshot.OutputBias;
        return true;
    }

    private void Forward(
        ReadOnlySpan<double> features,
        double[] hiddenPreActivation,
        double[] hiddenActivation,
        out double outputPreActivation
    )
    {
        outputPreActivation = _outputBias;
        for (var h = 0; h < _hiddenSize; h++)
        {
            var sum = _hiddenBias[h];
            var row = _inputToHidden[h];
            for (var i = 0; i < _inputSize; i++)
            {
                sum += row[i] * features[i];
            }

            hiddenPreActivation[h] = sum;
            var activated = sum > 0d ? sum : 0d;
            hiddenActivation[h] = activated;
            outputPreActivation += _hiddenToOutput[h] * activated;
        }
    }

    private void ValidateFeatures(ReadOnlySpan<double> features)
    {
        if (features.Length != _inputSize)
        {
            throw new InvalidOperationException(
                $"Feature vector mismatch. Expected {_inputSize}, received {features.Length}."
            );
        }
    }
}

internal sealed record OnlineDenseNetworkSnapshot(
    int InputSize,
    int HiddenSize,
    double LearningRate,
    double L2Regularization,
    double[][] InputToHidden,
    double[] HiddenBias,
    double[] HiddenToOutput,
    double OutputBias
);
