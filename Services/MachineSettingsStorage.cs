using System;
using System.IO;
using System.Text.Json;

namespace GRBL_Lathe_Control.Services;

public sealed record MachineSettingsStorageEntry(
    int SelectedSpindleSpeed,
    int SpindleMaxSpeed,
    int? LatheSpindleMaxSpeed,
    int? MillSpindleMaxSpeed,
    string? SelectedWorkCoordinateSystem,
    string XJogFeedInput,
    string YJogFeedInput,
    string ZJogFeedInput,
    string AJogFeedInput,
    string BJogFeedInput,
    double SelectedXJogStep,
    double SelectedYJogStep,
    double SelectedZJogStep,
    double SelectedAJogStep,
    double SelectedBJogStep,
    string ToolChangeXInput,
    string ToolChangeYInput,
    string ToolChangeSafeZInput,
    string? ProbeStartZInput,
    string ProbeTravelInput,
    string ProbeFeedInput,
    string ProbeFineFeedInput,
    string ProbeRetractInput,
    string? ProbeMinimumMachineZInput = null,
    string? ProbeMinimumClearanceInput = null,
    string? SelectedPort = null,
    string? BaudRateInput = null,
    string? DiameterTouchOffInput = null,
    string? PartProbeTravelInput = null,
    string? PartProbeFeedInput = null,
    string? PartProbeFineFeedInput = null,
    string? PartProbeRetractInput = null,
    string? PartProbeTipDiameterInput = null,
    string? PartProbeZOffsetInput = null,
    string? PartProbeMoveFeedInput = null,
    bool? PartProbeSetZeroAfterProbe = null,
    string? PartProbeCornerXDirection = null,
    string? PartProbeCornerYDirection = null,
    string? StraightEdgeDirection = null,
    string? StraightEdgeTouchCountInput = null,
    string? StraightEdgeSpacingInput = null,
    string? StraightEdgePullOffInput = null,
    string? StraightEdgeZLiftInput = null,
    string? SurfaceGridXCountInput = null,
    string? SurfaceGridYCountInput = null,
    string? SurfaceGridXSpacingInput = null,
    string? SurfaceGridYSpacingInput = null,
    string? ReferencePlateXInput = null,
    string? ReferencePlateYInput = null,
    string? ToolSetterXInput = null,
    string? ToolSetterYInput = null,
    string? ToolSetterProbeZInput = null,
    string? ToolSetterOffsetInput = null,
    bool? UseToolSetter = null);

public static class MachineSettingsStorage
{
    private const string AppDataFolderName = "GRBL Sender";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName,
            "machine-settings.json");

    public static MachineSettingsStorageEntry? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var fileContent = File.ReadAllText(FilePath);
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return null;
        }

        return JsonSerializer.Deserialize<MachineSettingsStorageEntry>(fileContent, JsonSerializerOptions);
    }

    public static void Save(MachineSettingsStorageEntry settings)
    {
        var directoryPath = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var serializedSettings = JsonSerializer.Serialize(settings, JsonSerializerOptions);
        File.WriteAllText(FilePath, serializedSettings);
    }
}
