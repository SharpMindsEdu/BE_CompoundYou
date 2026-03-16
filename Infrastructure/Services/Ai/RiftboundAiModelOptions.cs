namespace Infrastructure.Services.Ai;

public sealed class RiftboundAiModelOptions
{
    public const string SectionName = "RiftboundAiModel";

    public bool Enabled { get; set; } = true;

    public bool TrainingEnabled { get; set; } = true;

    public double ExplorationRate { get; set; } = 0.12d;

    public bool PersistModelToDisk { get; set; } = true;

    public int AutosaveIntervalSeconds { get; set; } = 30;

    public string ModelFilePath { get; set; } = "data/riftbound-training/embedded-model.json";

    public bool CaptureTrainingData { get; set; } = true;

    public string TrainingDataDirectory { get; set; } = "data/riftbound-training";

    public int MinSamplesForDeckBuild { get; set; } = 50;
}
