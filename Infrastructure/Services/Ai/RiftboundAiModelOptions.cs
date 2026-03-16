namespace Infrastructure.Services.Ai;

public sealed class RiftboundAiModelOptions
{
    public const string SectionName = "RiftboundAiModel";

    public bool Enabled { get; set; } = true;

    public bool TrainingEnabled { get; set; } = true;

    public bool UseNeuralNetwork { get; set; } = true;

    public double ExplorationRate { get; set; } = 0.12d;

    public int ActionNetworkHiddenSize { get; set; } = 48;

    public int DeckNetworkHiddenSize { get; set; } = 32;

    public double ActionLearningRate { get; set; } = 0.02d;

    public double DeckLearningRate { get; set; } = 0.015d;

    public double L2Regularization { get; set; } = 0.0005d;

    public double NeuralScoreWeight { get; set; } = 0.65d;

    public double CounterfactualPenaltyScale { get; set; } = 0.35d;

    public int MinActionSamplesForNeuralInference { get; set; } = 24;

    public int MinDeckSamplesForNeuralInference { get; set; } = 24;

    public int NetworkSeed { get; set; } = 1337;

    public bool PersistModelToDisk { get; set; } = true;

    public int AutosaveIntervalSeconds { get; set; } = 30;

    public string ModelFilePath { get; set; } = "data/riftbound-training/embedded-model.json";

    public bool CaptureTrainingData { get; set; } = true;

    public string TrainingDataDirectory { get; set; } = "data/riftbound-training";

    public int MinSamplesForDeckBuild { get; set; } = 50;
}
