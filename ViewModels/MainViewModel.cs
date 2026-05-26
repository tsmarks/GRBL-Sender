using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GRBL_Lathe_Control.Infrastructure;
using GRBL_Lathe_Control.Models;
using GRBL_Lathe_Control.Services;
using Microsoft.Win32;

namespace GRBL_Lathe_Control.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const double MinimumZProbeLimitClearance = 0.1d;

    private readonly Dispatcher _dispatcher;
    private readonly GrblClient _grblClient = new();
    private readonly MachineMode _machineMode;

    private CancellationTokenSource? _programCancellation;
    private GCodeProgram? _loadedProgram;

    private string _selectedPort = string.Empty;
    private string _baudRateInput = "115200";
    private string _connectionStatus = "Disconnected";
    private string _controllerState = "Offline";
    private string _workXInput = "0";
    private string _workYInput = "0";
    private string _workZInput = "0";
    private string _workAInput = "0";
    private string _workBInput = "0";
    private string _diameterTouchOffInput = "0";
    private string _goToXInput = "1";
    private string _goToYInput = "1";
    private string _goToZInput = "0";
    private string _newToolNumberInput = string.Empty;
    private string _xJogFeedInput = "200";
    private string _yJogFeedInput = "200";
    private string _zJogFeedInput = "400";
    private string _aJogFeedInput = "200";
    private string _bJogFeedInput = "200";
    private string _toolChangeXInput = "0";
    private string _toolChangeYInput = "0";
    private string _toolChangeSafeZInput = "0";
    private string _referencePlateXInput = "0";
    private string _referencePlateYInput = "0";
    private string _probeStartZInput = "0";
    private string _toolSetterXInput = "0";
    private string _toolSetterYInput = "0";
    private string _toolSetterProbeZInput = "0";
    private string _toolSetterOffsetInput = "0";
    private string _probeTravelInput = "50";
    private string _probeFeedInput = "100";
    private string _probeFineFeedInput = "25";
    private string _probeRetractInput = "2";
    private string _probeMinimumMachineZInput = string.Empty;
    private string _probeMinimumClearanceInput = "0.1";
    private string _partProbeTravelInput = "25";
    private string _partProbeFeedInput = "100";
    private string _partProbeFineFeedInput = "25";
    private string _partProbeRetractInput = "2";
    private string _partProbeTipDiameterInput = "0";
    private string _partProbeZOffsetInput = "0";
    private string _partProbeMoveFeedInput = "400";
    private string _partProbeCornerXDirection = "X-";
    private string _partProbeCornerYDirection = "Y-";
    private string _straightEdgeDirection = "X-";
    private string _straightEdgeTouchCountInput = "2";
    private string _straightEdgeSpacingInput = "25";
    private string _straightEdgePullOffInput = "2";
    private string _straightEdgeZLiftInput = "0";
    private string _surfaceGridXCountInput = "3";
    private string _surfaceGridYCountInput = "3";
    private string _surfaceGridXSpacingInput = "10";
    private string _surfaceGridYSpacingInput = "10";
    private string _partProbeResultText = "No part probe run yet.";
    private string _spindleMaxSpeedInput = "1000";
    private string _latheSpindleMaxSpeedInput = "1000";
    private string _millSpindleMaxSpeedInput = "1000";
    private string _selectedWorkCoordinateSystem = "G54";
    private string _terminalCommandInput = string.Empty;
    private string _programPath = "No file loaded";
    private double _machineX;
    private double _machineY;
    private double _machineZ;
    private double _machineA;
    private double _machineB;
    private double _workX;
    private double _workY;
    private double _workZ;
    private double _workA;
    private double _workB;
    private bool _xLimitPinHigh;
    private bool _yLimitPinHigh;
    private bool _zLimitPinHigh;
    private bool _probePinHigh;
    private bool _isConnected;
    private bool _isProgramRunning;
    private bool _isProgramPaused;
    private double _selectedXJogStep = 0.1;
    private double _selectedYJogStep = 0.1;
    private double _selectedZJogStep = 1;
    private double _selectedAJogStep = 1;
    private double _selectedBJogStep = 1;
    private int _latheSpindleMaxSpeed = 1000;
    private int _millSpindleMaxSpeed = 1000;
    private int _spindleMaxSpeed = 1000;
    private int _selectedSpindleSpeed = 500;
    private int _feedOverridePercent = 100;
    private int _lastKnownFeedOverridePercent = 100;
    private bool _isUpdatingFeedOverrideFromController;
    private int _executedProgramLines;
    private double _programProgressPercent;
    private IReadOnlyList<ToolPathSegment> _toolPathSegments = Array.Empty<ToolPathSegment>();
    private CancellationTokenSource? _feedOverrideAdjustmentCancellation;
    private bool _isKeyboardControlEnabled;
    private bool _isToolOffsetsLocked = true;
    private bool _partProbeSetZeroAfterProbe = true;
    private bool _isProbeNormallyClosed;
    private bool _useToolSetter;
    private bool _suppressProbePinModeWrite;
    private bool _suppressMachineSettingsPersistence;
    private bool _suppressToolOffsetPersistence;
    private int _activeToolNumber;
    private char _keyboardJogAxis = 'X';
    private string _lastAppliedWorkCoordinateSystem = string.Empty;
    private double? _millProbeReferenceWorkZ;
    private double? _millLastProbeWorkZ;
    private ProbeTouch? _lastProbeTouch;
    private int _lastProbeSequence;
    private double? _lastPartProbeXMinus;
    private double? _lastPartProbeXPlus;
    private double? _lastPartProbeYMinus;
    private double? _lastPartProbeYPlus;
    private PartProbeMeasuredEdge? _lastMeasuredPartEdge;
    private PartProbeMeasuredEdge? _partProbeMeasureA;
    private PartProbeMeasuredEdge? _partProbeMeasureB;

    public MainViewModel(MachineMode machineMode = MachineMode.Lathe)
    {
        _dispatcher = Application.Current.Dispatcher;
        _machineMode = machineMode;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        AddToolCommand = new RelayCommand(AddTool, CanAddTool);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => IsConnected);
        ZeroXCommand = new AsyncRelayCommand(() => SetWorkCoordinateAsync(xValue: 0), CanOffsetOrJog);
        ZeroYCommand = new AsyncRelayCommand(() => SetWorkCoordinateAsync(yValue: 0), CanOffsetOrJog);
        ZeroZCommand = new AsyncRelayCommand(() => SetWorkCoordinateAsync(zValue: 0), CanOffsetOrJog);
        ZeroACommand = new AsyncRelayCommand(() => SetWorkCoordinateAsync(aValue: 0), CanOffsetOrJog);
        ZeroBCommand = new AsyncRelayCommand(() => SetWorkCoordinateAsync(bValue: 0), CanOffsetOrJog);
        ZeroAllCommand = new AsyncRelayCommand(() => SetWorkCoordinateAsync(xValue: 0, zValue: 0), CanOffsetOrJog);
        HomeCommand = new AsyncRelayCommand(HomeAsync, CanOffsetOrJog);
        SoftResetCommand = new AsyncRelayCommand(SoftResetAsync, () => IsConnected);
        UnlockCommand = new AsyncRelayCommand(UnlockAsync, () => IsConnected);
        SendTerminalCommandCommand = new AsyncRelayCommand(SendTerminalCommandAsync, CanSendTerminalCommand);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        ReadControllerSettingsCommand = new AsyncRelayCommand(() => ReadControllerSettingsAsync(), () => IsConnected && !IsProgramRunning);
        SetSpindleMaxSpeedCommand = new RelayCommand(SetSpindleMaxSpeed, CanSetSpindleMaxSpeed);
        ApplySpindleSpeedCommand = new AsyncRelayCommand(ApplySpindleSpeedAsync, CanAdjustManualSpindle);
        StopSpindleCommand = new AsyncRelayCommand(StopSpindleAsync, CanAdjustManualSpindle);
        SetWorkXCommand = new AsyncRelayCommand(SetWorkXAsync, CanOffsetOrJog);
        SetWorkYCommand = new AsyncRelayCommand(SetWorkYAsync, CanOffsetOrJog);
        SetWorkZCommand = new AsyncRelayCommand(SetWorkZAsync, CanOffsetOrJog);
        SetWorkACommand = new AsyncRelayCommand(SetWorkAAsync, CanOffsetOrJog);
        SetWorkBCommand = new AsyncRelayCommand(SetWorkBAsync, CanOffsetOrJog);
        SetXFromDiameterCommand = new AsyncRelayCommand(SetXFromDiameterAsync, CanOffsetOrJog);
        GoToXCommand = new AsyncRelayCommand(GoToXAsync, CanOffsetOrJog);
        GoToYCommand = new AsyncRelayCommand(GoToYAsync, CanOffsetOrJog);
        GoToZCommand = new AsyncRelayCommand(GoToZAsync, CanOffsetOrJog);
        GoToRadiusPlusOneCommand = new AsyncRelayCommand(GoToRadiusPlusOneAsync, CanOffsetOrJog);
        JogXPositiveCommand = new AsyncRelayCommand(() => JogAxisAsync("X", SelectedXJogStep, XJogFeedInput), CanOffsetOrJog);
        JogXNegativeCommand = new AsyncRelayCommand(() => JogAxisAsync("X", -SelectedXJogStep, XJogFeedInput), CanOffsetOrJog);
        JogYPositiveCommand = new AsyncRelayCommand(() => JogAxisAsync("Y", SelectedYJogStep, YJogFeedInput), CanOffsetOrJog);
        JogYNegativeCommand = new AsyncRelayCommand(() => JogAxisAsync("Y", -SelectedYJogStep, YJogFeedInput), CanOffsetOrJog);
        JogXYNorthwestCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, x: -SelectedXYJogStep, y: SelectedXYJogStep), CanOffsetOrJog);
        JogXYNorthCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, y: SelectedXYJogStep), CanOffsetOrJog);
        JogXYNortheastCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, x: SelectedXYJogStep, y: SelectedXYJogStep), CanOffsetOrJog);
        JogXYWestCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, x: -SelectedXYJogStep), CanOffsetOrJog);
        JogXYEastCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, x: SelectedXYJogStep), CanOffsetOrJog);
        JogXYSouthwestCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, x: -SelectedXYJogStep, y: -SelectedXYJogStep), CanOffsetOrJog);
        JogXYSouthCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, y: -SelectedXYJogStep), CanOffsetOrJog);
        JogXYSoutheastCommand = new AsyncRelayCommand(() => JogAxesAsync("X/Y", XYJogFeedInput, x: SelectedXYJogStep, y: -SelectedXYJogStep), CanOffsetOrJog);
        JogZPositiveCommand = new AsyncRelayCommand(() => JogAxisAsync("Z", SelectedZJogStep, ZJogFeedInput), CanOffsetOrJog);
        JogZNegativeCommand = new AsyncRelayCommand(() => JogAxisAsync("Z", -SelectedZJogStep, ZJogFeedInput), CanOffsetOrJog);
        JogAPositiveCommand = new AsyncRelayCommand(() => JogAxisAsync("A", SelectedAJogStep, AJogFeedInput), CanOffsetOrJog);
        JogANegativeCommand = new AsyncRelayCommand(() => JogAxisAsync("A", -SelectedAJogStep, AJogFeedInput), CanOffsetOrJog);
        JogBPositiveCommand = new AsyncRelayCommand(() => JogAxisAsync("B", SelectedBJogStep, BJogFeedInput), CanOffsetOrJog);
        JogBNegativeCommand = new AsyncRelayCommand(() => JogAxisAsync("B", -SelectedBJogStep, BJogFeedInput), CanOffsetOrJog);
        CaptureToolChangePositionCommand = new RelayCommand(CaptureToolChangePosition, () => IsMillMode && IsConnected);
        CaptureReferencePlatePositionCommand = new RelayCommand(CaptureReferencePlatePosition, () => IsMillMode && IsConnected);
        CaptureProbeStartZCommand = new RelayCommand(CaptureProbeStartZ, () => IsMillMode && IsConnected);
        CaptureToolSetterPositionCommand = new RelayCommand(CaptureToolSetterPosition, () => IsMillMode && IsConnected);
        CaptureToolSetterProbeZCommand = new RelayCommand(CaptureToolSetterProbeZ, () => IsMillMode && IsConnected);
        GoToToolChangeCommand = new AsyncRelayCommand(GoToToolChangeAsync, () => IsMillMode && CanOffsetOrJog());
        GoSafeZCommand = new AsyncRelayCommand(GoSafeZAsync, () => IsMillMode && CanOffsetOrJog());
        GoToXYZeroCommand = new AsyncRelayCommand(GoToXYZeroAsync, () => IsMillMode && CanOffsetOrJog());
        CalibrateToolProbePlateCommand = new AsyncRelayCommand(CalibrateToolProbePlateAsync, () => IsMillMode && CanOffsetOrJog());
        CalibrateToolSetterOffsetCommand = new AsyncRelayCommand(CalibrateToolSetterOffsetAsync, () => IsMillMode && CanOffsetOrJog());
        RunToolProbeCommand = new AsyncRelayCommand(RunToolProbeAsync, () => IsMillMode && CanOffsetOrJog());
        ProbeXNegativeEdgeCommand = new AsyncRelayCommand(() => ProbePartEdgeAsync("X", -1), () => IsMillMode && CanOffsetOrJog());
        ProbeXPositiveEdgeCommand = new AsyncRelayCommand(() => ProbePartEdgeAsync("X", 1), () => IsMillMode && CanOffsetOrJog());
        ProbeYNegativeEdgeCommand = new AsyncRelayCommand(() => ProbePartEdgeAsync("Y", -1), () => IsMillMode && CanOffsetOrJog());
        ProbeYPositiveEdgeCommand = new AsyncRelayCommand(() => ProbePartEdgeAsync("Y", 1), () => IsMillMode && CanOffsetOrJog());
        ProbeZTouchCommand = new AsyncRelayCommand(ProbePartZTouchAsync, () => IsMillMode && CanOffsetOrJog());
        ProbeXYCornerCommand = new AsyncRelayCommand(ProbePartXYCornerAsync, () => IsMillMode && CanOffsetOrJog());
        ProbeHoleCenterCommand = new AsyncRelayCommand(ProbeHoleCenterAsync, () => IsMillMode && CanOffsetOrJog());
        CalculateOutsideCylinderCommand = new AsyncRelayCommand(CalculateOutsideCylinderFromTouchesAsync, () => IsMillMode);
        SetPartProbeMeasureACommand = new RelayCommand(() => SetPartProbeMeasurePoint(isMeasureA: true), CanSetPartProbeMeasurePoint);
        SetPartProbeMeasureBCommand = new RelayCommand(() => SetPartProbeMeasurePoint(isMeasureA: false), CanSetPartProbeMeasurePoint);
        ClearPartProbeMeasurementCommand = new RelayCommand(ClearPartProbeMeasurement);
        SetPartProbeMeasurementCenterZeroCommand = new AsyncRelayCommand(SetPartProbeMeasurementCenterZeroAsync, CanSetPartProbeMeasurementCenterZero);
        RunStraightEdgeProbeCommand = new AsyncRelayCommand(RunStraightEdgeProbeAsync, () => IsMillMode && CanOffsetOrJog());
        RunSurfaceGridProbeCommand = new AsyncRelayCommand(RunSurfaceGridProbeAsync, () => IsMillMode && CanOffsetOrJog());
        LoadProgramCommand = new RelayCommand(LoadProgram, () => !IsProgramRunning);
        StartProgramCommand = new AsyncRelayCommand(StartProgramAsync, CanStartProgram);
        PauseResumeProgramCommand = new AsyncRelayCommand(PauseResumeProgramAsync, CanPauseProgram);
        StopProgramCommand = new AsyncRelayCommand(StopProgramAsync, CanStopProgram);

        _grblClient.StatusReceived += OnGrblStatusReceived;
        _grblClient.MessageReceived += OnGrblMessageReceived;

        LoadPersistedMachineSettings();

        if (IsLatheMode)
        {
            LoadPersistedToolOffsets();
            EnsureMasterToolEntry();
        }

        RefreshPorts();
    }

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public ObservableCollection<string> ControllerLog { get; } = new();

    public ObservableCollection<ToolOffsetEntryViewModel> ToolOffsets { get; } = new();

    public IReadOnlyList<double> XJogSteps { get; } = [0.01, 0.05, 0.1, 0.5, 1, 5, 10];

    public IReadOnlyList<double> ZJogSteps { get; } = [0.01, 0.05, 0.1, 0.5, 1, 5, 10, 50, 100];

    public IReadOnlyList<double> LinearJogSteps { get; } = [0.01, 0.05, 0.1, 0.5, 1, 5, 10, 50, 100];

    public IReadOnlyList<double> RotaryJogSteps { get; } = [0.1, 0.5, 1, 5, 10, 45, 90];

    public IReadOnlyList<string> WorkCoordinateSystems { get; } = ["G54", "G55", "G56", "G57", "G58", "G59"];

    public IReadOnlyList<string> PartProbeXDirections { get; } = ["X-", "X+"];

    public IReadOnlyList<string> PartProbeYDirections { get; } = ["Y-", "Y+"];

    public IReadOnlyList<string> PartProbeEdgeDirections { get; } = ["X-", "X+", "Y-", "Y+"];

    public RelayCommand RefreshPortsCommand { get; }

    public RelayCommand AddToolCommand { get; }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    public AsyncRelayCommand ZeroXCommand { get; }

    public AsyncRelayCommand ZeroYCommand { get; }

    public AsyncRelayCommand ZeroZCommand { get; }

    public AsyncRelayCommand ZeroACommand { get; }

    public AsyncRelayCommand ZeroBCommand { get; }

    public AsyncRelayCommand ZeroAllCommand { get; }

    public AsyncRelayCommand HomeCommand { get; }

    public AsyncRelayCommand SoftResetCommand { get; }

    public AsyncRelayCommand UnlockCommand { get; }

    public AsyncRelayCommand SendTerminalCommandCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public AsyncRelayCommand ReadControllerSettingsCommand { get; }

    public RelayCommand SetSpindleMaxSpeedCommand { get; }

    public AsyncRelayCommand ApplySpindleSpeedCommand { get; }

    public AsyncRelayCommand StopSpindleCommand { get; }

    public AsyncRelayCommand SetWorkXCommand { get; }

    public AsyncRelayCommand SetWorkYCommand { get; }

    public AsyncRelayCommand SetWorkZCommand { get; }

    public AsyncRelayCommand SetWorkACommand { get; }

    public AsyncRelayCommand SetWorkBCommand { get; }

    public AsyncRelayCommand SetXFromDiameterCommand { get; }

    public AsyncRelayCommand GoToXCommand { get; }

    public AsyncRelayCommand GoToYCommand { get; }

    public AsyncRelayCommand GoToZCommand { get; }

    public AsyncRelayCommand GoToRadiusPlusOneCommand { get; }

    public AsyncRelayCommand JogXPositiveCommand { get; }

    public AsyncRelayCommand JogXNegativeCommand { get; }

    public AsyncRelayCommand JogYPositiveCommand { get; }

    public AsyncRelayCommand JogYNegativeCommand { get; }

    public AsyncRelayCommand JogXYNorthwestCommand { get; }

    public AsyncRelayCommand JogXYNorthCommand { get; }

    public AsyncRelayCommand JogXYNortheastCommand { get; }

    public AsyncRelayCommand JogXYWestCommand { get; }

    public AsyncRelayCommand JogXYEastCommand { get; }

    public AsyncRelayCommand JogXYSouthwestCommand { get; }

    public AsyncRelayCommand JogXYSouthCommand { get; }

    public AsyncRelayCommand JogXYSoutheastCommand { get; }

    public AsyncRelayCommand JogZPositiveCommand { get; }

    public AsyncRelayCommand JogZNegativeCommand { get; }

    public AsyncRelayCommand JogAPositiveCommand { get; }

    public AsyncRelayCommand JogANegativeCommand { get; }

    public AsyncRelayCommand JogBPositiveCommand { get; }

    public AsyncRelayCommand JogBNegativeCommand { get; }

    public RelayCommand CaptureToolChangePositionCommand { get; }

    public RelayCommand CaptureReferencePlatePositionCommand { get; }

    public RelayCommand CaptureProbeStartZCommand { get; }

    public RelayCommand CaptureToolSetterPositionCommand { get; }

    public RelayCommand CaptureToolSetterProbeZCommand { get; }

    public AsyncRelayCommand GoToToolChangeCommand { get; }

    public AsyncRelayCommand GoSafeZCommand { get; }

    public AsyncRelayCommand GoToXYZeroCommand { get; }

    public AsyncRelayCommand CalibrateToolProbePlateCommand { get; }

    public AsyncRelayCommand CalibrateToolSetterOffsetCommand { get; }

    public AsyncRelayCommand RunToolProbeCommand { get; }

    public AsyncRelayCommand ProbeXNegativeEdgeCommand { get; }

    public AsyncRelayCommand ProbeXPositiveEdgeCommand { get; }

    public AsyncRelayCommand ProbeYNegativeEdgeCommand { get; }

    public AsyncRelayCommand ProbeYPositiveEdgeCommand { get; }

    public AsyncRelayCommand ProbeZTouchCommand { get; }

    public AsyncRelayCommand ProbeXYCornerCommand { get; }

    public AsyncRelayCommand ProbeHoleCenterCommand { get; }

    public AsyncRelayCommand CalculateOutsideCylinderCommand { get; }

    public RelayCommand SetPartProbeMeasureACommand { get; }

    public RelayCommand SetPartProbeMeasureBCommand { get; }

    public RelayCommand ClearPartProbeMeasurementCommand { get; }

    public AsyncRelayCommand SetPartProbeMeasurementCenterZeroCommand { get; }

    public AsyncRelayCommand RunStraightEdgeProbeCommand { get; }

    public AsyncRelayCommand RunSurfaceGridProbeCommand { get; }

    public RelayCommand LoadProgramCommand { get; }

    public AsyncRelayCommand StartProgramCommand { get; }

    public AsyncRelayCommand PauseResumeProgramCommand { get; }

    public AsyncRelayCommand StopProgramCommand { get; }

    public string SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
            {
                PersistMachineSettings();
                RefreshCommandStates();
            }
        }
    }

    public string BaudRateInput
    {
        get => _baudRateInput;
        set
        {
            if (SetProperty(ref _baudRateInput, value))
            {
                PersistMachineSettings();
                RefreshCommandStates();
            }
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string ControllerState
    {
        get => _controllerState;
        private set => SetProperty(ref _controllerState, value);
    }

    public MachineMode MachineMode => _machineMode;

    public bool IsLatheMode => _machineMode == MachineMode.Lathe;

    public bool IsMillMode => _machineMode == MachineMode.Mill;

    public string MachineModeDisplayName => IsLatheMode ? "Lathe" : "Mill";

    public double MachineX
    {
        get => _machineX;
        private set => SetProperty(ref _machineX, value);
    }

    public double MachineY
    {
        get => _machineY;
        private set => SetProperty(ref _machineY, value);
    }

    public double MachineZ
    {
        get => _machineZ;
        private set => SetProperty(ref _machineZ, value);
    }

    public double MachineA
    {
        get => _machineA;
        private set => SetProperty(ref _machineA, value);
    }

    public double MachineB
    {
        get => _machineB;
        private set => SetProperty(ref _machineB, value);
    }

    public double WorkX
    {
        get => _workX;
        private set
        {
            if (SetProperty(ref _workX, value))
            {
                OnPropertyChanged(nameof(CurrentWorkOffsetText));
                OnPropertyChanged(nameof(PreviewHorizontalPosition));
            }
        }
    }

    public double WorkY
    {
        get => _workY;
        private set
        {
            if (SetProperty(ref _workY, value))
            {
                OnPropertyChanged(nameof(CurrentWorkOffsetText));
                OnPropertyChanged(nameof(PreviewVerticalPosition));
            }
        }
    }

    public double WorkZ
    {
        get => _workZ;
        private set
        {
            if (SetProperty(ref _workZ, value))
            {
                OnPropertyChanged(nameof(CurrentWorkOffsetText));
                OnPropertyChanged(nameof(PreviewHorizontalPosition));
            }
        }
    }

    public double WorkA
    {
        get => _workA;
        private set => SetProperty(ref _workA, value);
    }

    public double WorkB
    {
        get => _workB;
        private set => SetProperty(ref _workB, value);
    }

    public int ActiveToolNumber
    {
        get => _activeToolNumber;
        private set
        {
            if (SetProperty(ref _activeToolNumber, value))
            {
                OnPropertyChanged(nameof(ActiveToolText));
                OnPropertyChanged(nameof(MasterRelativeOffsetText));
            }
        }
    }

    public string ActiveToolText => $"Active tool T{ActiveToolNumber}";

    public string MasterRelativeOffsetText
    {
        get
        {
            if (!TryGetStoredToolOffsets(ActiveToolNumber, out var xOffset, out var zOffset))
            {
                return $"T{ActiveToolNumber} tool-tip offset unavailable";
            }

            return $"T{ActiveToolNumber} tip vs T0: X {xOffset:0.###} mm | Z {zOffset:0.###} mm";
        }
    }

    public string CurrentWorkOffsetText => IsLatheMode
        ? $"Displayed work X {WorkX:0.###} mm | Z {WorkZ:0.###} mm"
        : $"Displayed work X {WorkX:0.###} mm | Y {WorkY:0.###} mm | Z {WorkZ:0.###} mm";

    public string MillToolProbeStatusText
    {
        get
        {
            if (!IsMillMode)
            {
                return string.Empty;
            }

            if (!_millProbeReferenceWorkZ.HasValue)
            {
                return "Plate reference not calibrated for this setup.";
            }

            if (!_millLastProbeWorkZ.HasValue ||
                Math.Abs(_millLastProbeWorkZ.Value - _millProbeReferenceWorkZ.Value) < 0.0005d)
            {
                return $"Plate reference touch saved at work Z {_millProbeReferenceWorkZ.Value:0.###} mm.";
            }

            return
                $"Plate reference work Z {_millProbeReferenceWorkZ.Value:0.###} mm | last probe work Z {_millLastProbeWorkZ.Value:0.###} mm.";
        }
    }

    public string PartProbeMeasurementText
    {
        get
        {
            var lastText = _lastMeasuredPartEdge is null
                ? "Last edge: none yet. Probe an X or Y edge first."
                : $"Last edge: {_lastMeasuredPartEdge.Label} surface at machine {_lastMeasuredPartEdge.Axis} {_lastMeasuredPartEdge.MachineSurface:0.###} mm.";
            var measureAText = _partProbeMeasureA is null
                ? "A: not set"
                : $"A: {_partProbeMeasureA.Label} machine {_partProbeMeasureA.Axis} {_partProbeMeasureA.MachineSurface:0.###}";
            var measureBText = _partProbeMeasureB is null
                ? "B: not set"
                : $"B: {_partProbeMeasureB.Label} machine {_partProbeMeasureB.Axis} {_partProbeMeasureB.MachineSurface:0.###}";

            if (_partProbeMeasureA is null || _partProbeMeasureB is null)
            {
                return $"{lastText}\n{measureAText} | {measureBText}";
            }

            if (!string.Equals(_partProbeMeasureA.Axis, _partProbeMeasureB.Axis, StringComparison.OrdinalIgnoreCase))
            {
                return $"{lastText}\n{measureAText} | {measureBText}\nMeasure A and B are on different axes. Probe two X edges or two Y edges.";
            }

            var centerMachine = GetPartProbeMeasurementCenterMachine();
            var centerWork = GetWorkCoordinateAtMachinePosition(_partProbeMeasureA.Axis, centerMachine);
            var distance = Math.Abs(_partProbeMeasureB.MachineSurface - _partProbeMeasureA.MachineSurface);
            return
                $"{lastText}\n{measureAText} | {measureBText}\n" +
                $"{_partProbeMeasureA.Axis} distance {distance:0.###} mm | center machine {_partProbeMeasureA.Axis} {centerMachine:0.###} | current work {_partProbeMeasureA.Axis} {centerWork:0.###}.";
        }
    }

    public double PreviewHorizontalPosition => IsLatheMode ? WorkZ : WorkX;

    public double PreviewVerticalPosition => IsLatheMode ? WorkX : WorkY;

    public string PreviewHorizontalAxisLabel => IsLatheMode ? "Z" : "X";

    public string PreviewVerticalAxisLabel => IsLatheMode ? "X" : "Y";

    public bool IsKeyboardControlEnabled
    {
        get => _isKeyboardControlEnabled;
        set
        {
            if (SetProperty(ref _isKeyboardControlEnabled, value))
            {
                OnPropertyChanged(nameof(KeyboardControlStatusText));
                OnPropertyChanged(nameof(IsKeyboardXAxisActive));
                OnPropertyChanged(nameof(IsKeyboardYAxisActive));
                OnPropertyChanged(nameof(IsKeyboardZAxisActive));
                OnPropertyChanged(nameof(IsKeyboardAAxisActive));
                OnPropertyChanged(nameof(IsKeyboardBAxisActive));
            }
        }
    }

    public string KeyboardControlStatusText => IsKeyboardControlEnabled ? "Keyboard control on" : "Keyboard control off";

    public string KeyboardAxisText => $"Axis {KeyboardJogAxis}";

    public bool IsKeyboardXAxisActive => IsKeyboardControlEnabled && KeyboardJogAxis == 'X';

    public bool IsKeyboardYAxisActive => IsKeyboardControlEnabled && KeyboardJogAxis == 'Y';

    public bool IsKeyboardZAxisActive => IsKeyboardControlEnabled && KeyboardJogAxis == 'Z';

    public bool IsKeyboardAAxisActive => IsKeyboardControlEnabled && KeyboardJogAxis == 'A';

    public bool IsKeyboardBAxisActive => IsKeyboardControlEnabled && KeyboardJogAxis == 'B';

    public string KeyboardStepText => $"Step {GetActiveKeyboardStep():0.###} mm";

    public string KeyboardFeedText => $"Feed {GetActiveKeyboardFeedText()}";

    public bool IsToolOffsetsLocked
    {
        get => _isToolOffsetsLocked;
        set => SetProperty(ref _isToolOffsetsLocked, value);
    }

    public bool XLimitPinHigh
    {
        get => _xLimitPinHigh;
        private set => SetProperty(ref _xLimitPinHigh, value);
    }

    public bool YLimitPinHigh
    {
        get => _yLimitPinHigh;
        private set => SetProperty(ref _yLimitPinHigh, value);
    }

    public bool ZLimitPinHigh
    {
        get => _zLimitPinHigh;
        private set => SetProperty(ref _zLimitPinHigh, value);
    }

    public bool ProbePinHigh
    {
        get => _probePinHigh;
        private set => SetProperty(ref _probePinHigh, value);
    }

    public bool CanManualSpindleControl => IsConnected && !IsProgramRunning;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(CanManualSpindleControl));
                RefreshCommandStates();
            }
        }
    }

    public bool IsProgramRunning
    {
        get => _isProgramRunning;
        private set
        {
            if (SetProperty(ref _isProgramRunning, value))
            {
                OnPropertyChanged(nameof(PauseResumeLabel));
                OnPropertyChanged(nameof(ProgramSummaryText));
                OnPropertyChanged(nameof(CanManualSpindleControl));
                RefreshCommandStates();
            }
        }
    }

    public bool IsProgramPaused
    {
        get => _isProgramPaused;
        private set
        {
            if (SetProperty(ref _isProgramPaused, value))
            {
                OnPropertyChanged(nameof(PauseResumeLabel));
                OnPropertyChanged(nameof(ProgramSummaryText));
                RefreshCommandStates();
            }
        }
    }

    public string PauseResumeLabel => IsProgramPaused ? "Resume" : "Pause";

    public string WorkXInput
    {
        get => _workXInput;
        set => SetProperty(ref _workXInput, value);
    }

    public string WorkYInput
    {
        get => _workYInput;
        set => SetProperty(ref _workYInput, value);
    }

    public string WorkZInput
    {
        get => _workZInput;
        set => SetProperty(ref _workZInput, value);
    }

    public string WorkAInput
    {
        get => _workAInput;
        set => SetProperty(ref _workAInput, value);
    }

    public string WorkBInput
    {
        get => _workBInput;
        set => SetProperty(ref _workBInput, value);
    }

    public string DiameterTouchOffInput
    {
        get => _diameterTouchOffInput;
        set
        {
            if (SetProperty(ref _diameterTouchOffInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string GoToXInput
    {
        get => _goToXInput;
        set => SetProperty(ref _goToXInput, value);
    }

    public string GoToYInput
    {
        get => _goToYInput;
        set => SetProperty(ref _goToYInput, value);
    }

    public string GoToZInput
    {
        get => _goToZInput;
        set => SetProperty(ref _goToZInput, value);
    }

    public string NewToolNumberInput
    {
        get => _newToolNumberInput;
        set
        {
            if (SetProperty(ref _newToolNumberInput, value))
            {
                AddToolCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string XJogFeedInput
    {
        get => _xJogFeedInput;
        set
        {
            if (SetProperty(ref _xJogFeedInput, value))
            {
                OnPropertyChanged(nameof(XYJogFeedInput));
                OnPropertyChanged(nameof(KeyboardFeedText));
                PersistMachineSettings();
            }
        }
    }

    public string YJogFeedInput
    {
        get => _yJogFeedInput;
        set
        {
            if (SetProperty(ref _yJogFeedInput, value))
            {
                OnPropertyChanged(nameof(XYJogFeedInput));
                OnPropertyChanged(nameof(KeyboardFeedText));
                PersistMachineSettings();
            }
        }
    }

    public string ZJogFeedInput
    {
        get => _zJogFeedInput;
        set
        {
            if (SetProperty(ref _zJogFeedInput, value))
            {
                OnPropertyChanged(nameof(KeyboardFeedText));
                PersistMachineSettings();
            }
        }
    }

    public string AJogFeedInput
    {
        get => _aJogFeedInput;
        set
        {
            if (SetProperty(ref _aJogFeedInput, value))
            {
                OnPropertyChanged(nameof(ABJogFeedInput));
                OnPropertyChanged(nameof(KeyboardFeedText));
                PersistMachineSettings();
            }
        }
    }

    public string BJogFeedInput
    {
        get => _bJogFeedInput;
        set
        {
            if (SetProperty(ref _bJogFeedInput, value))
            {
                OnPropertyChanged(nameof(ABJogFeedInput));
                OnPropertyChanged(nameof(KeyboardFeedText));
                PersistMachineSettings();
            }
        }
    }

    public string XYJogFeedInput
    {
        get => XJogFeedInput;
        set
        {
            XJogFeedInput = value;
            YJogFeedInput = value;
            OnPropertyChanged();
        }
    }

    public string ABJogFeedInput
    {
        get => AJogFeedInput;
        set
        {
            AJogFeedInput = value;
            BJogFeedInput = value;
            OnPropertyChanged();
        }
    }

    public string ToolChangeXInput
    {
        get => _toolChangeXInput;
        set
        {
            if (SetProperty(ref _toolChangeXInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ToolChangeYInput
    {
        get => _toolChangeYInput;
        set
        {
            if (SetProperty(ref _toolChangeYInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ToolChangeSafeZInput
    {
        get => _toolChangeSafeZInput;
        set
        {
            if (SetProperty(ref _toolChangeSafeZInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ReferencePlateXInput
    {
        get => _referencePlateXInput;
        set
        {
            if (SetProperty(ref _referencePlateXInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ReferencePlateYInput
    {
        get => _referencePlateYInput;
        set
        {
            if (SetProperty(ref _referencePlateYInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeStartZInput
    {
        get => _probeStartZInput;
        set
        {
            if (SetProperty(ref _probeStartZInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ToolSetterXInput
    {
        get => _toolSetterXInput;
        set
        {
            if (SetProperty(ref _toolSetterXInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ToolSetterYInput
    {
        get => _toolSetterYInput;
        set
        {
            if (SetProperty(ref _toolSetterYInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ToolSetterProbeZInput
    {
        get => _toolSetterProbeZInput;
        set
        {
            if (SetProperty(ref _toolSetterProbeZInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ToolSetterOffsetInput
    {
        get => _toolSetterOffsetInput;
        set
        {
            if (SetProperty(ref _toolSetterOffsetInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeTravelInput
    {
        get => _probeTravelInput;
        set
        {
            if (SetProperty(ref _probeTravelInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeFeedInput
    {
        get => _probeFeedInput;
        set
        {
            if (SetProperty(ref _probeFeedInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeFineFeedInput
    {
        get => _probeFineFeedInput;
        set
        {
            if (SetProperty(ref _probeFineFeedInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeRetractInput
    {
        get => _probeRetractInput;
        set
        {
            if (SetProperty(ref _probeRetractInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeMinimumMachineZInput
    {
        get => _probeMinimumMachineZInput;
        set
        {
            if (SetProperty(ref _probeMinimumMachineZInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string ProbeMinimumClearanceInput
    {
        get => _probeMinimumClearanceInput;
        set
        {
            if (SetProperty(ref _probeMinimumClearanceInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeTravelInput
    {
        get => _partProbeTravelInput;
        set
        {
            if (SetProperty(ref _partProbeTravelInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeFeedInput
    {
        get => _partProbeFeedInput;
        set
        {
            if (SetProperty(ref _partProbeFeedInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeFineFeedInput
    {
        get => _partProbeFineFeedInput;
        set
        {
            if (SetProperty(ref _partProbeFineFeedInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeRetractInput
    {
        get => _partProbeRetractInput;
        set
        {
            if (SetProperty(ref _partProbeRetractInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeTipDiameterInput
    {
        get => _partProbeTipDiameterInput;
        set
        {
            if (SetProperty(ref _partProbeTipDiameterInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeZOffsetInput
    {
        get => _partProbeZOffsetInput;
        set
        {
            if (SetProperty(ref _partProbeZOffsetInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeMoveFeedInput
    {
        get => _partProbeMoveFeedInput;
        set
        {
            if (SetProperty(ref _partProbeMoveFeedInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public bool PartProbeSetZeroAfterProbe
    {
        get => _partProbeSetZeroAfterProbe;
        set
        {
            if (SetProperty(ref _partProbeSetZeroAfterProbe, value))
            {
                PersistMachineSettings();
                CalculateOutsideCylinderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsProbeNormallyClosed
    {
        get => _isProbeNormallyClosed;
        set
        {
            if (SetProperty(ref _isProbeNormallyClosed, value))
            {
                OnPropertyChanged(nameof(ProbePinModeText));

                if (!_suppressProbePinModeWrite && IsConnected)
                {
                    _ = SetProbePinInvertAsync(value);
                }
            }
        }
    }

    public string ProbePinModeText => IsProbeNormallyClosed ? "Probe mode: NC ($6=1)" : "Probe mode: NO ($6=0)";

    public bool UseToolSetter
    {
        get => _useToolSetter;
        set
        {
            if (SetProperty(ref _useToolSetter, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeCornerXDirection
    {
        get => _partProbeCornerXDirection;
        set
        {
            if (SetProperty(ref _partProbeCornerXDirection, NormalizeProbeDirection(value, "X-")))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeCornerYDirection
    {
        get => _partProbeCornerYDirection;
        set
        {
            if (SetProperty(ref _partProbeCornerYDirection, NormalizeProbeDirection(value, "Y-")))
            {
                PersistMachineSettings();
            }
        }
    }

    public string StraightEdgeDirection
    {
        get => _straightEdgeDirection;
        set
        {
            if (SetProperty(ref _straightEdgeDirection, NormalizeProbeDirection(value, "X-")))
            {
                PersistMachineSettings();
            }
        }
    }

    public string StraightEdgeTouchCountInput
    {
        get => _straightEdgeTouchCountInput;
        set
        {
            if (SetProperty(ref _straightEdgeTouchCountInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string StraightEdgeSpacingInput
    {
        get => _straightEdgeSpacingInput;
        set
        {
            if (SetProperty(ref _straightEdgeSpacingInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string StraightEdgePullOffInput
    {
        get => _straightEdgePullOffInput;
        set
        {
            if (SetProperty(ref _straightEdgePullOffInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string StraightEdgeZLiftInput
    {
        get => _straightEdgeZLiftInput;
        set
        {
            if (SetProperty(ref _straightEdgeZLiftInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string SurfaceGridXCountInput
    {
        get => _surfaceGridXCountInput;
        set
        {
            if (SetProperty(ref _surfaceGridXCountInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string SurfaceGridYCountInput
    {
        get => _surfaceGridYCountInput;
        set
        {
            if (SetProperty(ref _surfaceGridYCountInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string SurfaceGridXSpacingInput
    {
        get => _surfaceGridXSpacingInput;
        set
        {
            if (SetProperty(ref _surfaceGridXSpacingInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string SurfaceGridYSpacingInput
    {
        get => _surfaceGridYSpacingInput;
        set
        {
            if (SetProperty(ref _surfaceGridYSpacingInput, value))
            {
                PersistMachineSettings();
            }
        }
    }

    public string PartProbeResultText
    {
        get => _partProbeResultText;
        private set => SetProperty(ref _partProbeResultText, value);
    }

    public string SpindleMaxSpeedInput
    {
        get => _spindleMaxSpeedInput;
        set
        {
            if (SetProperty(ref _spindleMaxSpeedInput, value))
            {
                SetSpindleMaxSpeedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LatheSpindleMaxSpeedInput
    {
        get => _latheSpindleMaxSpeedInput;
        set => SetProperty(ref _latheSpindleMaxSpeedInput, value);
    }

    public string MillSpindleMaxSpeedInput
    {
        get => _millSpindleMaxSpeedInput;
        set => SetProperty(ref _millSpindleMaxSpeedInput, value);
    }

    public int SpindleMaxSpeed
    {
        get => _spindleMaxSpeed;
        private set
        {
            var normalizedValue = Math.Max(1, value);
            if (SetProperty(ref _spindleMaxSpeed, normalizedValue))
            {
                if (IsLatheMode)
                {
                    _latheSpindleMaxSpeed = normalizedValue;
                    LatheSpindleMaxSpeedInput = normalizedValue.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    _millSpindleMaxSpeed = normalizedValue;
                    MillSpindleMaxSpeedInput = normalizedValue.ToString(CultureInfo.InvariantCulture);
                }

                if (SelectedSpindleSpeed > normalizedValue)
                {
                    SelectedSpindleSpeed = normalizedValue;
                }

                OnPropertyChanged(nameof(SelectedSpindleSpeedText));
                PersistMachineSettings();
            }
        }
    }

    public int SelectedSpindleSpeed
    {
        get => _selectedSpindleSpeed;
        set
        {
            var clampedValue = Math.Clamp(value, 0, SpindleMaxSpeed);
            if (SetProperty(ref _selectedSpindleSpeed, clampedValue))
            {
                OnPropertyChanged(nameof(SelectedSpindleSpeedText));
            }
        }
    }

    public string SelectedSpindleSpeedText => $"S{SelectedSpindleSpeed} / max S{SpindleMaxSpeed}";

    public string SelectedWorkCoordinateSystem
    {
        get => _selectedWorkCoordinateSystem;
        set
        {
            var normalizedValue = NormalizeWorkCoordinateSystem(value);
            if (SetProperty(ref _selectedWorkCoordinateSystem, normalizedValue))
            {
                _lastAppliedWorkCoordinateSystem = string.Empty;
                PersistMachineSettings();
            }
        }
    }

    public int FeedOverridePercent
    {
        get => _feedOverridePercent;
        set
        {
            var clampedValue = Math.Clamp(value, 25, 200);
            if (SetProperty(ref _feedOverridePercent, clampedValue))
            {
                OnPropertyChanged(nameof(FeedOverrideText));

                if (!_isUpdatingFeedOverrideFromController)
                {
                    ScheduleFeedOverrideUpdate();
                }
            }
        }
    }

    public string FeedOverrideText => $"{FeedOverridePercent}%";

    public double SelectedXJogStep
    {
        get => _selectedXJogStep;
        set
        {
            if (SetProperty(ref _selectedXJogStep, value))
            {
                OnPropertyChanged(nameof(SelectedXYJogStep));
                OnPropertyChanged(nameof(KeyboardStepText));
                PersistMachineSettings();
            }
        }
    }

    public double SelectedYJogStep
    {
        get => _selectedYJogStep;
        set
        {
            if (SetProperty(ref _selectedYJogStep, value))
            {
                OnPropertyChanged(nameof(SelectedXYJogStep));
                OnPropertyChanged(nameof(KeyboardStepText));
                PersistMachineSettings();
            }
        }
    }

    public double SelectedZJogStep
    {
        get => _selectedZJogStep;
        set
        {
            if (SetProperty(ref _selectedZJogStep, value))
            {
                OnPropertyChanged(nameof(KeyboardStepText));
                PersistMachineSettings();
            }
        }
    }

    public double SelectedAJogStep
    {
        get => _selectedAJogStep;
        set
        {
            if (SetProperty(ref _selectedAJogStep, value))
            {
                OnPropertyChanged(nameof(SelectedABJogStep));
                OnPropertyChanged(nameof(KeyboardStepText));
                PersistMachineSettings();
            }
        }
    }

    public double SelectedBJogStep
    {
        get => _selectedBJogStep;
        set
        {
            if (SetProperty(ref _selectedBJogStep, value))
            {
                OnPropertyChanged(nameof(SelectedABJogStep));
                OnPropertyChanged(nameof(KeyboardStepText));
                PersistMachineSettings();
            }
        }
    }

    public double SelectedXYJogStep
    {
        get => SelectedXJogStep;
        set
        {
            SelectedXJogStep = value;
            SelectedYJogStep = value;
            OnPropertyChanged();
        }
    }

    public double SelectedABJogStep
    {
        get => SelectedAJogStep;
        set
        {
            SelectedAJogStep = value;
            SelectedBJogStep = value;
            OnPropertyChanged();
        }
    }

    private char KeyboardJogAxis
    {
        get => _keyboardJogAxis;
        set
        {
            if (SetProperty(ref _keyboardJogAxis, value))
            {
                OnPropertyChanged(nameof(KeyboardAxisText));
                OnPropertyChanged(nameof(IsKeyboardXAxisActive));
                OnPropertyChanged(nameof(IsKeyboardYAxisActive));
                OnPropertyChanged(nameof(IsKeyboardZAxisActive));
                OnPropertyChanged(nameof(IsKeyboardAAxisActive));
                OnPropertyChanged(nameof(IsKeyboardBAxisActive));
                OnPropertyChanged(nameof(KeyboardStepText));
                OnPropertyChanged(nameof(KeyboardFeedText));
            }
        }
    }

    public string ProgramPath
    {
        get => _programPath;
        private set => SetProperty(ref _programPath, value);
    }

    public string TerminalCommandInput
    {
        get => _terminalCommandInput;
        set
        {
            if (SetProperty(ref _terminalCommandInput, value))
            {
                SendTerminalCommandCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int ExecutedProgramLines
    {
        get => _executedProgramLines;
        private set
        {
            if (SetProperty(ref _executedProgramLines, value))
            {
                OnPropertyChanged(nameof(ProgramProgressText));
            }
        }
    }

    public double ProgramProgressPercent
    {
        get => _programProgressPercent;
        private set => SetProperty(ref _programProgressPercent, value);
    }

    public IReadOnlyList<ToolPathSegment> ToolPathSegments
    {
        get => _toolPathSegments;
        private set => SetProperty(ref _toolPathSegments, value);
    }

    public string ProgramSummaryText
    {
        get
        {
            if (_loadedProgram is null)
            {
                return "No G-code file loaded.";
            }

            var runtimeState = IsProgramRunning
                ? IsProgramPaused ? "Paused" : "Running"
                : "Ready";

            return $"{_loadedProgram.DisplayName} | {_loadedProgram.ExecutableLineCount} executable lines | {runtimeState}";
        }
    }

    public string ProgramProgressText
    {
        get
        {
            if (_loadedProgram is null)
            {
                return "Load a .nc, .tap, .gcode, or .txt file.";
            }

            return $"{ExecutedProgramLines} / {_loadedProgram.ExecutableLineCount} lines sent";
        }
    }

    public void Dispose()
    {
        foreach (var toolOffsetEntry in ToolOffsets)
        {
            UnregisterToolOffsetEntry(toolOffsetEntry);
        }

        _feedOverrideAdjustmentCancellation?.Cancel();
        _feedOverrideAdjustmentCancellation?.Dispose();
        _grblClient.StatusReceived -= OnGrblStatusReceived;
        _grblClient.MessageReceived -= OnGrblMessageReceived;
        _programCancellation?.Cancel();
        _grblClient.Dispose();
    }

    private void RefreshPorts()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(portName => portName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port);
        }

        if (string.IsNullOrWhiteSpace(SelectedPort) || !ports.Contains(SelectedPort, StringComparer.OrdinalIgnoreCase))
        {
            SelectedPort = ports.FirstOrDefault() ?? string.Empty;
        }

        AddLog(ports.Length == 0 ? "No serial ports detected." : $"Detected {ports.Length} serial port(s).");
        RefreshCommandStates();
    }

    private bool CanConnect()
    {
        return !IsConnected &&
               !string.IsNullOrWhiteSpace(SelectedPort) &&
               int.TryParse(BaudRateInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var baudRate) &&
               baudRate > 0;
    }

    private bool CanOffsetOrJog()
    {
        return IsConnected && !IsProgramRunning;
    }

    private async Task CalibrateToolProbePlateAsync()
    {
        if (!IsMillMode)
        {
            return;
        }

        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before calibrating the plate reference.");
            return;
        }

        if (!TryGetMillProbeSettings(
                out var referencePlateX,
                out var referencePlateY,
                out var safeZ,
                out var probeStartZ,
                out var probeTravel,
                out var probeFeed,
                out var probeFineFeed,
                out var probeRetract))
        {
            return;
        }

        try
        {
            await CalibrateToolProbePlateCoreAsync(
                referencePlateX,
                referencePlateY,
                safeZ,
                probeStartZ,
                probeTravel,
                probeFeed,
                probeFineFeed,
                probeRetract);
        }
        catch (Exception exception)
        {
            ShowOperationError("Plate reference calibration failed", exception);
        }
    }

    private async Task<(double MachineZ, double WorkZ)> CalibrateToolProbePlateCoreAsync(
        double referencePlateX,
        double referencePlateY,
        double safeZ,
        double probeStartZ,
        double probeTravel,
        double probeFeed,
        double probeFineFeed,
        double probeRetract)
    {
        AddLog("Starting mill plate-reference calibration probe.");
        var probeResult = await ExecuteMillProbeCycleAsync(
            referencePlateX,
            referencePlateY,
            safeZ,
            probeStartZ,
            probeTravel,
            probeFeed,
            probeFineFeed,
            probeRetract);

        _millProbeReferenceWorkZ = probeResult.WorkZ;
        _millLastProbeWorkZ = probeResult.WorkZ;
        OnPropertyChanged(nameof(MillToolProbeStatusText));
        AddLog($"Calibrated mill plate reference from probe touch at work Z {probeResult.WorkZ:0.###} mm (machine Z {probeResult.MachineZ:0.###} mm).");
        await ReturnMillProbeToSafeZAsync(safeZ);
        return probeResult;
    }

    private async Task ProbePartEdgeAsync(string axisLetter, int direction)
    {
        if (!TryGetPartProbeSettings(
                out var travel,
                out var feed,
                out var fineFeed,
                out var retract,
                out var tipRadius,
                out _))
        {
            return;
        }

        try
        {
            await ProbePartEdgeCoreAsync(axisLetter, direction, travel, feed, fineFeed, retract, tipRadius);
        }
        catch (Exception exception)
        {
            ShowOperationError("Part edge probe failed", exception);
        }
    }

    private async Task ProbePartEdgeCoreAsync(
        string axisLetter,
        int direction,
        double travel,
        double feed,
        double fineFeed,
        double retract,
        double tipRadius)
    {
        var normalizedAxis = axisLetter.ToUpperInvariant();
        AddLog($"Starting part probe {normalizedAxis}{FormatDirection(direction)} edge.");
        var touch = await ExecutePartProbeTouchAsync(normalizedAxis, direction, travel, feed, fineFeed, retract);
        StoreDirectionalPartTouch(normalizedAxis, direction, touch);
        StoreMeasuredPartEdge(normalizedAxis, direction, touch, tipRadius);

        if (PartProbeSetZeroAfterProbe)
        {
            var compensatedCurrent = direction < 0 ? tipRadius : -tipRadius;
            if (normalizedAxis == "X")
            {
                await SetWorkCoordinateAsync(xValue: compensatedCurrent);
            }
            else
            {
                await SetWorkCoordinateAsync(yValue: compensatedCurrent);
            }
        }

        await MoveRelativeForAxisAsync(normalizedAxis, -direction * retract, feed);
        PartProbeResultText =
            $"{normalizedAxis}{FormatDirection(direction)} edge touched at {FormatAxisValue(normalizedAxis, GetWorkAxis(touch, normalizedAxis))}; " +
            $"surface estimate {FormatAxisValue(normalizedAxis, GetCompensatedSurfaceCoordinate(normalizedAxis, direction, touch, tipRadius))}.";
        AddLog(PartProbeResultText);
    }

    private async Task ProbePartZTouchAsync()
    {
        if (!TryGetPartProbeSettings(
                out var travel,
                out var feed,
                out var fineFeed,
                out var retract,
                out _,
                out var zOffset))
        {
            return;
        }

        try
        {
            AddLog("Starting part Z touch probe.");
            var touch = await ExecutePartProbeTouchAsync("Z", -1, travel, feed, fineFeed, retract);

            if (PartProbeSetZeroAfterProbe)
            {
                await SetWorkCoordinateAsync(zValue: zOffset);
            }

            await MoveRelativeForAxisAsync("Z", retract, feed);
            PartProbeResultText = $"Z touch completed at work Z {touch.WorkZ:0.###} mm; Z zero target {zOffset:0.###} mm.";
            AddLog(PartProbeResultText);
        }
        catch (Exception exception)
        {
            ShowOperationError("Part Z touch probe failed", exception);
        }
    }

    private async Task ProbePartXYCornerAsync()
    {
        if (!TryGetProbeDirection(PartProbeCornerXDirection, out var xAxis, out var xDirection) ||
            !TryGetProbeDirection(PartProbeCornerYDirection, out var yAxis, out var yDirection) ||
            xAxis != "X" ||
            yAxis != "Y")
        {
            ShowValidationError("Choose one X direction and one Y direction for the corner probe.");
            return;
        }

        if (!TryGetPartProbeSettings(
                out var travel,
                out var feed,
                out var fineFeed,
                out var retract,
                out var tipRadius,
                out _))
        {
            return;
        }

        try
        {
            AddLog($"Starting XY corner probe using {PartProbeCornerXDirection} and {PartProbeCornerYDirection}.");
            await ProbePartEdgeCoreAsync("X", xDirection, travel, feed, fineFeed, retract, tipRadius);
            await ProbePartEdgeCoreAsync("Y", yDirection, travel, feed, fineFeed, retract, tipRadius);
            PartProbeResultText = $"XY corner probe complete using {PartProbeCornerXDirection} and {PartProbeCornerYDirection}.";
            AddLog(PartProbeResultText);
        }
        catch (Exception exception)
        {
            ShowOperationError("XY corner probe failed", exception);
        }
    }

    private async Task ProbeHoleCenterAsync()
    {
        if (!TryGetPartProbeSettings(
                out var travel,
                out var feed,
                out var fineFeed,
                out var retract,
                out var tipRadius,
                out _))
        {
            return;
        }

        try
        {
            var startX = WorkX;
            var startY = WorkY;
            AddLog("Starting inside hole center probe from the current approximate center.");

            var xMinus = await ProbeTouchFromStartAsync("X", -1, startX, startY, travel, feed, fineFeed, retract);
            var xPlus = await ProbeTouchFromStartAsync("X", 1, startX, startY, travel, feed, fineFeed, retract);
            var yMinus = await ProbeTouchFromStartAsync("Y", -1, startX, startY, travel, feed, fineFeed, retract);
            var yPlus = await ProbeTouchFromStartAsync("Y", 1, startX, startY, travel, feed, fineFeed, retract);

            _lastPartProbeXMinus = xMinus.MachineX;
            _lastPartProbeXPlus = xPlus.MachineX;
            _lastPartProbeYMinus = yMinus.MachineY;
            _lastPartProbeYPlus = yPlus.MachineY;

            var centerX = (xMinus.WorkX + xPlus.WorkX) / 2d;
            var centerY = (yMinus.WorkY + yPlus.WorkY) / 2d;
            var diameterX = Math.Abs(xPlus.WorkX - xMinus.WorkX) + (tipRadius * 2d);
            var diameterY = Math.Abs(yPlus.WorkY - yMinus.WorkY) + (tipRadius * 2d);
            var averageDiameter = (diameterX + diameterY) / 2d;

            if (PartProbeSetZeroAfterProbe)
            {
                await SetWorkCoordinateAsync(xValue: startX - centerX, yValue: startY - centerY);
            }

            PartProbeResultText =
                $"Hole center X {centerX:0.###}, Y {centerY:0.###}; inside radius {averageDiameter / 2d:0.###} mm " +
                $"(diameters X {diameterX:0.###}, Y {diameterY:0.###}).";
            AddLog(PartProbeResultText);
        }
        catch (Exception exception)
        {
            ShowOperationError("Hole center probe failed", exception);
        }
    }

    private async Task CalculateOutsideCylinderFromTouchesAsync()
    {
        if (!TryParseDouble(PartProbeTipDiameterInput, "probe tip diameter", out var tipDiameter) || tipDiameter < 0)
        {
            ShowValidationError("Enter a non-negative probe tip diameter.");
            return;
        }

        if (!_lastPartProbeXMinus.HasValue ||
            !_lastPartProbeXPlus.HasValue ||
            !_lastPartProbeYMinus.HasValue ||
            !_lastPartProbeYPlus.HasValue)
        {
            ShowValidationError("Probe or record X-, X+, Y-, and Y+ touches before calculating an outside cylinder.");
            return;
        }

        var centerX = ((_lastPartProbeXMinus.Value + _lastPartProbeXPlus.Value) / 2d) + (WorkX - MachineX);
        var centerY = ((_lastPartProbeYMinus.Value + _lastPartProbeYPlus.Value) / 2d) + (WorkY - MachineY);
        var diameterX = Math.Max(0, Math.Abs(_lastPartProbeXPlus.Value - _lastPartProbeXMinus.Value) - tipDiameter);
        var diameterY = Math.Max(0, Math.Abs(_lastPartProbeYPlus.Value - _lastPartProbeYMinus.Value) - tipDiameter);
        var averageDiameter = (diameterX + diameterY) / 2d;

        if (PartProbeSetZeroAfterProbe)
        {
            if (!CanOffsetOrJog())
            {
                ShowValidationError("Connect to the controller and stop any running program before setting cylinder center zero.");
                return;
            }

            await SetWorkCoordinateAsync(xValue: WorkX - centerX, yValue: WorkY - centerY);
        }

        PartProbeResultText =
            $"Outside cylinder center X {centerX:0.###}, Y {centerY:0.###}; outside radius {averageDiameter / 2d:0.###} mm " +
            $"(diameters X {diameterX:0.###}, Y {diameterY:0.###}).";
        AddLog(PartProbeResultText);
    }

    private async Task RunStraightEdgeProbeAsync()
    {
        if (!TryGetPartProbeSettings(
                out var travel,
                out var feed,
                out var fineFeed,
                out _,
                out _,
                out _) ||
            !TryGetProbeDirection(StraightEdgeDirection, out var probeAxis, out var direction) ||
            !TryGetPartProbeMoveFeed(out var moveFeed) ||
            !TryParsePositiveInt(StraightEdgeTouchCountInput, out var touchCount) ||
            !TryParseDouble(StraightEdgeSpacingInput, "straight edge spacing", out var spacing) ||
            !TryParseDouble(StraightEdgePullOffInput, "straight edge pull-off", out var pullOff) ||
            !TryParseDouble(StraightEdgeZLiftInput, "straight edge Z lift", out var zLift))
        {
            return;
        }

        if (touchCount < 2 || spacing == 0 || pullOff <= 0 || zLift < 0)
        {
            ShowValidationError("Use at least 2 touches, non-zero spacing, positive pull-off, and a non-negative Z lift for the straight edge macro.");
            return;
        }

        try
        {
            var parallelAxis = probeAxis == "X" ? "Y" : "X";
            var touches = new List<(double Parallel, double Touched)>();
            AddLog($"Starting straight edge macro on {StraightEdgeDirection} with {touchCount} touches.");

            for (var index = 0; index < touchCount; index++)
            {
                var touch = await ExecutePartProbeTouchAsync(probeAxis, direction, travel, feed, fineFeed, pullOff);
                touches.Add((GetCurrentWorkAxis(parallelAxis), GetWorkAxis(touch, probeAxis)));
                await MoveRelativeForAxisAsync(probeAxis, -direction * pullOff, moveFeed);

                if (index < touchCount - 1)
                {
                    if (zLift > 0)
                    {
                        await MoveRelativeForAxisAsync("Z", zLift, moveFeed);
                    }

                    await MoveRelativeForAxisAsync(parallelAxis, spacing, moveFeed);

                    if (zLift > 0)
                    {
                        await MoveRelativeForAxisAsync("Z", -zLift, moveFeed);
                    }
                }
            }

            var first = touches[0];
            var last = touches[^1];
            var run = last.Parallel - first.Parallel;
            var rise = last.Touched - first.Touched;
            var angle = Math.Abs(run) < 0.0005d ? 0 : Math.Atan2(rise, run) * 180d / Math.PI;
            PartProbeResultText =
                $"Straight edge {StraightEdgeDirection}: {touchCount} touches over {run:0.###} mm, " +
                $"edge drift {rise:0.###} mm, angle {angle:0.###} deg.";
            AddLog(PartProbeResultText);
        }
        catch (Exception exception)
        {
            ShowOperationError("Straight edge macro failed", exception);
        }
    }

    private async Task RunSurfaceGridProbeAsync()
    {
        if (!TryGetPartProbeSettings(
                out var travel,
                out var feed,
                out var fineFeed,
                out var retract,
                out _,
                out _) ||
            !TryParsePositiveInt(SurfaceGridXCountInput, out var xCount) ||
            !TryParsePositiveInt(SurfaceGridYCountInput, out var yCount) ||
            !TryGetPartProbeMoveFeed(out var moveFeed) ||
            !TryParseDouble(SurfaceGridXSpacingInput, "surface grid X spacing", out var xSpacing) ||
            !TryParseDouble(SurfaceGridYSpacingInput, "surface grid Y spacing", out var ySpacing))
        {
            return;
        }

        if (xCount <= 0 || yCount <= 0 || xSpacing == 0 || ySpacing == 0)
        {
            ShowValidationError("Use positive grid counts and non-zero X/Y spacing for the surface grid.");
            return;
        }

        try
        {
            var points = new List<double>();
            AddLog($"Starting surface grid Z probe: {xCount} by {yCount}.");

            for (var row = 0; row < yCount; row++)
            {
                for (var column = 0; column < xCount; column++)
                {
                    var touch = await ExecutePartProbeTouchAsync("Z", -1, travel, feed, fineFeed, retract);
                    points.Add(touch.WorkZ);
                    await MoveRelativeForAxisAsync("Z", retract, moveFeed);

                    if (column < xCount - 1)
                    {
                        var direction = row % 2 == 0 ? 1 : -1;
                        await MoveRelativeForAxisAsync("X", direction * xSpacing, moveFeed);
                    }
                }

                if (row < yCount - 1)
                {
                    await MoveRelativeForAxisAsync("Y", ySpacing, moveFeed);
                }
            }

            var min = points.Min();
            var max = points.Max();
            var average = points.Average();
            PartProbeResultText =
                $"Surface grid complete: {points.Count} Z touches, min {min:0.###}, max {max:0.###}, " +
                $"range {max - min:0.###}, avg {average:0.###} mm.";
            AddLog(PartProbeResultText);
        }
        catch (Exception exception)
        {
            ShowOperationError("Surface grid probe failed", exception);
        }
    }

    private bool CanStartProgram()
    {
        return IsConnected && _loadedProgram is not null && !IsProgramRunning;
    }

    private bool CanPauseProgram()
    {
        return IsConnected && IsProgramRunning;
    }

    private bool CanStopProgram()
    {
        return IsConnected && IsProgramRunning;
    }

    private async Task ConnectAsync()
    {
        if (!int.TryParse(BaudRateInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var baudRate) || baudRate <= 0)
        {
            ShowValidationError("Enter a valid baud rate.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            ShowValidationError("Select a COM port before connecting.");
            return;
        }

        try
        {
            ConnectionStatus = $"Connecting to {SelectedPort}...";
            ControllerState = "Waiting for status";
            await _grblClient.ConnectAsync(SelectedPort, baudRate);
            IsConnected = true;
            _lastAppliedWorkCoordinateSystem = string.Empty;
            UpdateFeedOverrideFromController(100);
            XLimitPinHigh = false;
            YLimitPinHigh = false;
            ZLimitPinHigh = false;
            ProbePinHigh = false;
            ConnectionStatus = $"Connected to {SelectedPort}";
            AddLog($"Connected to {SelectedPort}.");
            await ReadControllerSettingsAsync(showErrors: false);
        }
        catch (Exception exception)
        {
            ConnectionStatus = "Disconnected";
            ControllerState = "Offline";
            ShowOperationError("Connection failed", exception);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            _programCancellation?.Cancel();
            await _grblClient.DisconnectAsync();
        }
        catch (Exception exception)
        {
            ShowOperationError("Disconnect failed", exception);
        }
        finally
        {
            _feedOverrideAdjustmentCancellation?.Cancel();
            IsConnected = false;
            _lastAppliedWorkCoordinateSystem = string.Empty;
            IsProgramRunning = false;
            IsProgramPaused = false;
            XLimitPinHigh = false;
            YLimitPinHigh = false;
            ZLimitPinHigh = false;
            ProbePinHigh = false;
            UpdateFeedOverrideFromController(100);
            ConnectionStatus = "Disconnected";
            ControllerState = "Offline";
        }
    }

    private async Task SetWorkXAsync()
    {
        if (!TryParseDouble(WorkXInput, "work X", out var xValue))
        {
            return;
        }

        await SetWorkCoordinateAsync(xValue: xValue);
    }

    private async Task SetWorkYAsync()
    {
        if (!TryParseDouble(WorkYInput, "work Y", out var yValue))
        {
            return;
        }

        await SetWorkCoordinateAsync(yValue: yValue);
    }

    private async Task SetWorkZAsync()
    {
        if (!TryParseDouble(WorkZInput, "work Z", out var zValue))
        {
            return;
        }

        await SetWorkCoordinateAsync(zValue: zValue);
    }

    private async Task SetWorkAAsync()
    {
        if (!TryParseDouble(WorkAInput, "work A", out var aValue))
        {
            return;
        }

        await SetWorkCoordinateAsync(aValue: aValue);
    }

    private async Task SetWorkBAsync()
    {
        if (!TryParseDouble(WorkBInput, "work B", out var bValue))
        {
            return;
        }

        await SetWorkCoordinateAsync(bValue: bValue);
    }

    private async Task HomeAsync()
    {
        try
        {
            AddLog("Starting homing cycle.");
            await _grblClient.HomeAsync();
        }
        catch (Exception exception)
        {
            ShowOperationError("Home failed", exception);
        }
    }

    private async Task SetXFromDiameterAsync()
    {
        if (!TryParseDouble(DiameterTouchOffInput, "touch-off diameter", out var diameter) || diameter < 0)
        {
            ShowValidationError("Enter a non-negative diameter for X touch-off.");
            return;
        }

        var xRadius = diameter / 2d;
        WorkXInput = xRadius.ToString("0.###", CultureInfo.InvariantCulture);
        await SetWorkCoordinateAsync(xValue: xRadius);
    }

    private async Task GoToXAsync()
    {
        if (!TryParseDouble(GoToXInput, "go-to X", out var targetX))
        {
            return;
        }

        await MoveToWorkCoordinateAsync(
            xValue: targetX,
            feedRateInput: XJogFeedInput,
            feedRateLabel: "X go-to feed",
            successMessage: $"Moving X to {targetX:0.###} mm.");
    }

    private async Task GoToZAsync()
    {
        if (!TryParseDouble(GoToZInput, "go-to Z", out var targetZ))
        {
            return;
        }

        await MoveToWorkCoordinateAsync(
            zValue: targetZ,
            feedRateInput: ZJogFeedInput,
            feedRateLabel: "Z go-to feed",
            successMessage: $"Moving Z to {targetZ:0.###} mm.");
    }

    private async Task GoToYAsync()
    {
        if (!TryParseDouble(GoToYInput, "go-to Y", out var targetY))
        {
            return;
        }

        await MoveToWorkCoordinateAsync(
            yValue: targetY,
            feedRateInput: YJogFeedInput,
            feedRateLabel: "Y go-to feed",
            successMessage: $"Moving Y to {targetY:0.###} mm.");
    }

    private async Task GoToRadiusPlusOneAsync()
    {
        if (!TryParseDouble(DiameterTouchOffInput, "touch-off diameter", out var diameter) || diameter < 0)
        {
            ShowValidationError("Enter a non-negative diameter to use Radius +1.");
            return;
        }

        var targetX = (diameter / 2d) + 1d;
        await MoveToWorkCoordinateAsync(
            xValue: targetX,
            feedRateInput: XJogFeedInput,
            feedRateLabel: "X go-to feed",
            successMessage: $"Moving X to radius +1 at {targetX:0.###} mm.");
    }

    private async Task MoveToWorkCoordinateAsync(
        double? xValue = null,
        double? yValue = null,
        double? zValue = null,
        double? aValue = null,
        double? bValue = null,
        string? feedRateInput = null,
        string feedRateLabel = "go-to feed",
        string successMessage = "Move command sent.")
    {
        if (!TryParseDouble(feedRateInput ?? string.Empty, feedRateLabel, out var feedRate) || feedRate <= 0)
        {
            ShowValidationError($"Enter a positive {feedRateLabel}.");
            return;
        }

        try
        {
            await EnsureSelectedWorkCoordinateSystemActiveAsync();
            await _grblClient.MoveToAsync(xValue, yValue, zValue, aValue, bValue, feedRate);
            AddLog(successMessage);
        }
        catch (Exception exception)
        {
            ShowOperationError("Go-to move failed", exception);
        }
    }

    private async Task SetWorkCoordinateAsync(
        double? xValue = null,
        double? yValue = null,
        double? zValue = null,
        double? aValue = null,
        double? bValue = null)
    {
        try
        {
            await _grblClient.SetWorkCoordinateOffsetAsync(
                xValue,
                yValue,
                zValue,
                aValue,
                bValue,
                workCoordinateSystem: SelectedWorkCoordinateSystem);
            await EnsureSelectedWorkCoordinateSystemActiveAsync();
            AddLog(BuildOffsetLogMessage(xValue, yValue, zValue, aValue, bValue, SelectedWorkCoordinateSystem));

            if (IsMillMode && zValue.HasValue)
            {
                ClearMillToolProbeCalibration("Mill plate reference cleared because work Z changed. Recalibrate with the reference tool.");
            }
        }
        catch (Exception exception)
        {
            ShowOperationError("Work offset update failed", exception);
        }
    }

    private async Task JogAxisAsync(string axisLetter, double distanceMillimeters, string feedRateInput)
    {
        await JogAxesAsync(axisLetter, feedRateInput, axisLetter switch
        {
            "X" => distanceMillimeters,
            _ => null
        }, axisLetter switch
        {
            "Y" => distanceMillimeters,
            _ => null
        }, axisLetter switch
        {
            "Z" => distanceMillimeters,
            _ => null
        }, axisLetter switch
        {
            "A" => distanceMillimeters,
            _ => null
        }, axisLetter switch
        {
            "B" => distanceMillimeters,
            _ => null
        });
    }

    private async Task JogAxesAsync(
        string axisLabel,
        string feedRateInput,
        double? x = null,
        double? y = null,
        double? z = null,
        double? a = null,
        double? b = null)
    {
        if (!TryParseDouble(feedRateInput, $"{axisLabel} jog feed", out var feedRate) || feedRate <= 0)
        {
            ShowValidationError($"Enter a positive jog feed for {axisLabel}.");
            return;
        }

        try
        {
            await _grblClient.JogAsync(x: x, y: y, z: z, a: a, b: b, feedRateMillimetersPerMinute: feedRate);
        }
        catch (Exception exception)
        {
            ShowOperationError($"Jog {axisLabel} failed", exception);
        }
    }

    private async Task ApplySpindleSpeedAsync()
    {
        try
        {
            await _grblClient.SetSpindleSpeedAsync(SelectedSpindleSpeed);
            AddLog(SelectedSpindleSpeed == 0
                ? "Spindle stopped."
                : $"Spindle command sent with S{SelectedSpindleSpeed}.");
            PersistMachineSettings();
        }
        catch (Exception exception)
        {
            ShowOperationError("Spindle update failed", exception);
        }
    }

    private async Task StopSpindleAsync()
    {
        try
        {
            await _grblClient.StopSpindleAsync();
            AddLog("Spindle stop command sent.");
        }
        catch (Exception exception)
        {
            ShowOperationError("Spindle stop failed", exception);
        }
    }

    private async Task RunToolProbeAsync()
    {
        if (!IsMillMode)
        {
            return;
        }

        if (!_millProbeReferenceWorkZ.HasValue)
        {
            ShowValidationError("Calibrate the plate reference with the master tool before probing another tool.");
            return;
        }

        if (!TryGetMillProbeSettings(
                out var referencePlateX,
                out var referencePlateY,
                out var safeZ,
                out var probeStartZ,
                out var probeTravel,
                out var probeFeed,
                out var probeFineFeed,
                out var probeRetract))
        {
            return;
        }

        var probeX = referencePlateX;
        var probeY = referencePlateY;
        var targetTouchWorkZ = _millProbeReferenceWorkZ.Value;
        var toolSetterOffset = 0d;
        if (UseToolSetter)
        {
            if (!TryGetToolSetterSettings(out probeX, out probeY, out probeStartZ, out toolSetterOffset))
            {
                return;
            }

            targetTouchWorkZ = _millProbeReferenceWorkZ.Value + toolSetterOffset;
        }

        try
        {
            AddLog(UseToolSetter ? "Starting mill tool probe cycle on tool setter." : "Starting mill tool probe cycle on reference plate.");
            var workZBeforeProbe = WorkZ;
            var probeResult = await ExecuteMillProbeCycleAsync(
                probeX,
                probeY,
                safeZ,
                probeStartZ,
                probeTravel,
                probeFeed,
                probeFineFeed,
                probeRetract);
            var equivalentReferenceWorkZ = UseToolSetter
                ? probeResult.WorkZ - toolSetterOffset
                : probeResult.WorkZ;
            var workZAdjustment = _millProbeReferenceWorkZ.Value - equivalentReferenceWorkZ;
            await _grblClient.SetWorkCoordinateOffsetAsync(
                z: targetTouchWorkZ,
                workCoordinateSystem: SelectedWorkCoordinateSystem);
            await EnsureSelectedWorkCoordinateSystemActiveAsync();
            await ReturnMillProbeToSafeZAsync(safeZ);

            var desiredWorkZ = workZBeforeProbe + workZAdjustment;
            WorkZ = desiredWorkZ;
            WorkZInput = desiredWorkZ.ToString("0.###", CultureInfo.InvariantCulture);
            _millLastProbeWorkZ = equivalentReferenceWorkZ;
            OnPropertyChanged(nameof(MillToolProbeStatusText));
            AddLog(
                UseToolSetter
                    ? $"Tool setter probe complete. Setter touch was work Z {probeResult.WorkZ:0.###} mm with saved setter offset {toolSetterOffset:+0.###;-0.###;0} mm, equivalent plate touch {equivalentReferenceWorkZ:0.###} mm. Work Z adjusted by {workZAdjustment:+0.###;-0.###;0} mm; current work Z is {desiredWorkZ:0.###} mm."
                    : $"Tool probe complete. Tool touched at work Z {probeResult.WorkZ:0.###} mm (machine Z {probeResult.MachineZ:0.###} mm), so work Z was adjusted by {workZAdjustment:+0.###;-0.###;0} mm. Current work Z is {desiredWorkZ:0.###} mm.");
        }
        catch (Exception exception)
        {
            ShowOperationError("Tool probe failed", exception);
        }
    }

    private async Task CalibrateToolSetterOffsetAsync()
    {
        if (!IsMillMode)
        {
            return;
        }

        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before calibrating the tool setter offset.");
            return;
        }

        if (!TryGetMillProbeSettings(
                out var referencePlateX,
                out var referencePlateY,
                out var safeZ,
                out var probeStartZ,
                out var probeTravel,
                out var probeFeed,
                out var probeFineFeed,
                out var probeRetract) ||
            !TryGetToolSetterLocationSettings(
                out var toolSetterX,
                out var toolSetterY,
                out var toolSetterProbeZ))
        {
            return;
        }

        try
        {
            AddLog("Starting tool setter offset calibration.");
            await CalibrateToolProbePlateCoreAsync(
                referencePlateX,
                referencePlateY,
                safeZ,
                probeStartZ,
                probeTravel,
                probeFeed,
                probeFineFeed,
                probeRetract);

            var referenceProbeMode = PromptProbeModeSelection(
                "Plate reference is complete. If you changed to the probe input used for the reference/tool probe, choose the probe mode to use next.",
                "Reference Probe Mode");
            if (!referenceProbeMode.HasValue)
            {
                AddLog("Tool setter offset calibration canceled before the reference comparison probe.");
                return;
            }

            if (!await SetProbePinInvertAsync(referenceProbeMode.Value))
            {
                return;
            }

            AddLog("Running reference plate comparison probe for tool setter offset.");
            var referenceProbeResult = await ExecuteMillProbeCycleAsync(
                referencePlateX,
                referencePlateY,
                safeZ,
                probeStartZ,
                probeTravel,
                probeFeed,
                probeFineFeed,
                probeRetract);
            await ReturnMillProbeToSafeZAsync(safeZ);
            _millProbeReferenceWorkZ = referenceProbeResult.WorkZ;
            _millLastProbeWorkZ = referenceProbeResult.WorkZ;
            OnPropertyChanged(nameof(MillToolProbeStatusText));
            AddLog($"Updated mill plate reference for setter calibration to work Z {referenceProbeResult.WorkZ:0.###} mm.");

            var setterProbeMode = PromptProbeModeSelection(
                "Reference comparison probe is complete. If you changed to the tool setter input, choose the setter probe mode to use next.",
                "Tool Setter Probe Mode");
            if (!setterProbeMode.HasValue)
            {
                AddLog("Tool setter offset calibration canceled before the setter probe.");
                return;
            }

            if (!await SetProbePinInvertAsync(setterProbeMode.Value))
            {
                return;
            }

            AddLog("Running tool setter probe for offset comparison.");
            var setterProbeResult = await ExecuteMillProbeCycleAsync(
                toolSetterX,
                toolSetterY,
                safeZ,
                toolSetterProbeZ,
                probeTravel,
                probeFeed,
                probeFineFeed,
                probeRetract);
            await ReturnMillProbeToSafeZAsync(safeZ);

            var setterOffset = setterProbeResult.WorkZ - referenceProbeResult.WorkZ;
            ToolSetterOffsetInput = setterOffset.ToString("0.###", CultureInfo.InvariantCulture);
            UseToolSetter = true;
            _millLastProbeWorkZ = referenceProbeResult.WorkZ;
            OnPropertyChanged(nameof(MillToolProbeStatusText));
            AddLog(
                $"Tool setter offset calibrated. Reference plate touch was work Z {referenceProbeResult.WorkZ:0.###} mm, setter touch was work Z {setterProbeResult.WorkZ:0.###} mm, saved setter-minus-reference offset {setterOffset:+0.###;-0.###;0} mm.");
        }
        catch (Exception exception)
        {
            ShowOperationError("Tool setter offset calibration failed", exception);
        }
    }

    private static bool? PromptProbeModeSelection(string message, string title)
    {
        var result = MessageBox.Show(
            $"{message}\n\nYes = NC probe / $6=1\nNo = NO probe / $6=0\nCancel = stop this calibration",
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }

    public bool TryHandleKeyboardInput(Key key, ModifierKeys modifiers, bool isRepeat)
    {
        if (!IsKeyboardControlEnabled || modifiers != ModifierKeys.None)
        {
            return false;
        }

        switch (key)
        {
            case Key.Left:
                ExecuteKeyboardJog(positiveDirection: false);
                return true;
            case Key.Right:
                ExecuteKeyboardJog(positiveDirection: true);
                return true;
            case Key.Up:
                if (!isRepeat)
                {
                    AdjustKeyboardStep(1);
                }

                return true;
            case Key.Down:
                if (!isRepeat)
                {
                    AdjustKeyboardStep(-1);
                }

                return true;
            case Key.A:
                if (!isRepeat)
                {
                    KeyboardJogAxis = GetNextKeyboardAxis();
                }

                return true;
            case Key.OemComma:
                if (!isRepeat)
                {
                    AdjustKeyboardFeed(-10);
                }

                return true;
            case Key.OemPeriod:
                if (!isRepeat)
                {
                    AdjustKeyboardFeed(10);
                }

                return true;
            default:
                return false;
        }
    }

    private bool CanAddTool()
    {
        return TryParseToolNumber(NewToolNumberInput, out _);
    }

    private bool CanSetSpindleMaxSpeed()
    {
        return TryParsePositiveInt(SpindleMaxSpeedInput, out _);
    }

    private void SetSpindleMaxSpeed()
    {
        if (!TryParsePositiveInt(SpindleMaxSpeedInput, out var spindleMaxSpeed))
        {
            ShowValidationError("Enter a positive whole number for spindle max speed.");
            return;
        }

        SpindleMaxSpeed = spindleMaxSpeed;
        SpindleMaxSpeedInput = spindleMaxSpeed.ToString(CultureInfo.InvariantCulture);
        AddLog($"Spindle max speed set to S{SpindleMaxSpeed}.");
    }

    private void LoadPersistedMachineSettings()
    {
        try
        {
            var storedSettings = MachineSettingsStorage.Load();
            if (storedSettings is null)
            {
                return;
            }

            _suppressMachineSettingsPersistence = true;

            SelectedPort = storedSettings.SelectedPort ?? string.Empty;
            BaudRateInput = string.IsNullOrWhiteSpace(storedSettings.BaudRateInput)
                ? "115200"
                : storedSettings.BaudRateInput;
            var legacySpindleMax = storedSettings.SpindleMaxSpeed > 0 ? storedSettings.SpindleMaxSpeed : 1000;
            _latheSpindleMaxSpeed = storedSettings.LatheSpindleMaxSpeed.GetValueOrDefault(legacySpindleMax);
            _millSpindleMaxSpeed = storedSettings.MillSpindleMaxSpeed.GetValueOrDefault(legacySpindleMax);
            LatheSpindleMaxSpeedInput = _latheSpindleMaxSpeed.ToString(CultureInfo.InvariantCulture);
            MillSpindleMaxSpeedInput = _millSpindleMaxSpeed.ToString(CultureInfo.InvariantCulture);
            SpindleMaxSpeed = IsLatheMode ? _latheSpindleMaxSpeed : _millSpindleMaxSpeed;
            SpindleMaxSpeedInput = SpindleMaxSpeed.ToString(CultureInfo.InvariantCulture);
            SelectedSpindleSpeed = storedSettings.SelectedSpindleSpeed;
            SelectedWorkCoordinateSystem = string.IsNullOrWhiteSpace(storedSettings.SelectedWorkCoordinateSystem)
                ? "G54"
                : storedSettings.SelectedWorkCoordinateSystem;
            DiameterTouchOffInput = string.IsNullOrWhiteSpace(storedSettings.DiameterTouchOffInput)
                ? "0"
                : storedSettings.DiameterTouchOffInput;
            XJogFeedInput = storedSettings.XJogFeedInput;
            YJogFeedInput = storedSettings.YJogFeedInput;
            ZJogFeedInput = storedSettings.ZJogFeedInput;
            AJogFeedInput = storedSettings.AJogFeedInput;
            BJogFeedInput = storedSettings.BJogFeedInput;
            SelectedXJogStep = storedSettings.SelectedXJogStep > 0 ? storedSettings.SelectedXJogStep : XJogSteps[2];
            SelectedYJogStep = storedSettings.SelectedYJogStep > 0 ? storedSettings.SelectedYJogStep : LinearJogSteps[2];
            SelectedZJogStep = storedSettings.SelectedZJogStep > 0 ? storedSettings.SelectedZJogStep : LinearJogSteps[4];
            SelectedAJogStep = storedSettings.SelectedAJogStep > 0 ? storedSettings.SelectedAJogStep : RotaryJogSteps[2];
            SelectedBJogStep = storedSettings.SelectedBJogStep > 0 ? storedSettings.SelectedBJogStep : RotaryJogSteps[2];
            ToolChangeXInput = storedSettings.ToolChangeXInput;
            ToolChangeYInput = storedSettings.ToolChangeYInput;
            ToolChangeSafeZInput = storedSettings.ToolChangeSafeZInput;
            ReferencePlateXInput = string.IsNullOrWhiteSpace(storedSettings.ReferencePlateXInput)
                ? storedSettings.ToolChangeXInput
                : storedSettings.ReferencePlateXInput;
            ReferencePlateYInput = string.IsNullOrWhiteSpace(storedSettings.ReferencePlateYInput)
                ? storedSettings.ToolChangeYInput
                : storedSettings.ReferencePlateYInput;
            ProbeStartZInput = string.IsNullOrWhiteSpace(storedSettings.ProbeStartZInput)
                ? storedSettings.ToolChangeSafeZInput
                : storedSettings.ProbeStartZInput;
            ToolSetterXInput = string.IsNullOrWhiteSpace(storedSettings.ToolSetterXInput)
                ? ReferencePlateXInput
                : storedSettings.ToolSetterXInput;
            ToolSetterYInput = string.IsNullOrWhiteSpace(storedSettings.ToolSetterYInput)
                ? ReferencePlateYInput
                : storedSettings.ToolSetterYInput;
            ToolSetterProbeZInput = string.IsNullOrWhiteSpace(storedSettings.ToolSetterProbeZInput)
                ? ProbeStartZInput
                : storedSettings.ToolSetterProbeZInput;
            ToolSetterOffsetInput = string.IsNullOrWhiteSpace(storedSettings.ToolSetterOffsetInput)
                ? "0"
                : storedSettings.ToolSetterOffsetInput;
            UseToolSetter = storedSettings.UseToolSetter.GetValueOrDefault(false);
            ProbeTravelInput = storedSettings.ProbeTravelInput;
            ProbeFeedInput = storedSettings.ProbeFeedInput;
            ProbeFineFeedInput = string.IsNullOrWhiteSpace(storedSettings.ProbeFineFeedInput)
                ? storedSettings.ProbeFeedInput
                : storedSettings.ProbeFineFeedInput;
            ProbeRetractInput = storedSettings.ProbeRetractInput;
            ProbeMinimumMachineZInput = storedSettings.ProbeMinimumMachineZInput ?? string.Empty;
            ProbeMinimumClearanceInput = string.IsNullOrWhiteSpace(storedSettings.ProbeMinimumClearanceInput)
                ? "0.1"
                : storedSettings.ProbeMinimumClearanceInput;
            PartProbeTravelInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeTravelInput)
                ? "25"
                : storedSettings.PartProbeTravelInput;
            PartProbeFeedInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeFeedInput)
                ? "100"
                : storedSettings.PartProbeFeedInput;
            PartProbeFineFeedInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeFineFeedInput)
                ? "25"
                : storedSettings.PartProbeFineFeedInput;
            PartProbeRetractInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeRetractInput)
                ? "2"
                : storedSettings.PartProbeRetractInput;
            PartProbeTipDiameterInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeTipDiameterInput)
                ? "0"
                : storedSettings.PartProbeTipDiameterInput;
            PartProbeZOffsetInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeZOffsetInput)
                ? "0"
                : storedSettings.PartProbeZOffsetInput;
            PartProbeMoveFeedInput = string.IsNullOrWhiteSpace(storedSettings.PartProbeMoveFeedInput)
                ? "400"
                : storedSettings.PartProbeMoveFeedInput;
            PartProbeSetZeroAfterProbe = storedSettings.PartProbeSetZeroAfterProbe.GetValueOrDefault(true);
            PartProbeCornerXDirection = string.IsNullOrWhiteSpace(storedSettings.PartProbeCornerXDirection)
                ? "X-"
                : storedSettings.PartProbeCornerXDirection;
            PartProbeCornerYDirection = string.IsNullOrWhiteSpace(storedSettings.PartProbeCornerYDirection)
                ? "Y-"
                : storedSettings.PartProbeCornerYDirection;
            StraightEdgeDirection = string.IsNullOrWhiteSpace(storedSettings.StraightEdgeDirection)
                ? "X-"
                : storedSettings.StraightEdgeDirection;
            StraightEdgeTouchCountInput = string.IsNullOrWhiteSpace(storedSettings.StraightEdgeTouchCountInput)
                ? "2"
                : storedSettings.StraightEdgeTouchCountInput;
            StraightEdgeSpacingInput = string.IsNullOrWhiteSpace(storedSettings.StraightEdgeSpacingInput)
                ? "25"
                : storedSettings.StraightEdgeSpacingInput;
            StraightEdgePullOffInput = string.IsNullOrWhiteSpace(storedSettings.StraightEdgePullOffInput)
                ? "2"
                : storedSettings.StraightEdgePullOffInput;
            StraightEdgeZLiftInput = string.IsNullOrWhiteSpace(storedSettings.StraightEdgeZLiftInput)
                ? "0"
                : storedSettings.StraightEdgeZLiftInput;
            SurfaceGridXCountInput = string.IsNullOrWhiteSpace(storedSettings.SurfaceGridXCountInput)
                ? "3"
                : storedSettings.SurfaceGridXCountInput;
            SurfaceGridYCountInput = string.IsNullOrWhiteSpace(storedSettings.SurfaceGridYCountInput)
                ? "3"
                : storedSettings.SurfaceGridYCountInput;
            SurfaceGridXSpacingInput = string.IsNullOrWhiteSpace(storedSettings.SurfaceGridXSpacingInput)
                ? "10"
                : storedSettings.SurfaceGridXSpacingInput;
            SurfaceGridYSpacingInput = string.IsNullOrWhiteSpace(storedSettings.SurfaceGridYSpacingInput)
                ? "10"
                : storedSettings.SurfaceGridYSpacingInput;
            AddLog("Loaded persisted machine settings.");
        }
        catch (Exception exception)
        {
            AddLog($"Machine settings load failed: {exception.Message}");
        }
        finally
        {
            _suppressMachineSettingsPersistence = false;
        }
    }

    private void PersistMachineSettings()
    {
        if (_suppressMachineSettingsPersistence)
        {
            return;
        }

        try
        {
            MachineSettingsStorage.Save(new MachineSettingsStorageEntry(
                SelectedSpindleSpeed,
                SpindleMaxSpeed,
                _latheSpindleMaxSpeed,
                _millSpindleMaxSpeed,
                SelectedWorkCoordinateSystem,
                XJogFeedInput,
                YJogFeedInput,
                ZJogFeedInput,
                AJogFeedInput,
                BJogFeedInput,
                SelectedXJogStep,
                SelectedYJogStep,
                SelectedZJogStep,
                SelectedAJogStep,
                SelectedBJogStep,
                ToolChangeXInput,
                ToolChangeYInput,
                ToolChangeSafeZInput,
                ProbeStartZInput,
                ProbeTravelInput,
                ProbeFeedInput,
                ProbeFineFeedInput,
                ProbeRetractInput,
                ProbeMinimumMachineZInput,
                ProbeMinimumClearanceInput,
                SelectedPort,
                BaudRateInput,
                DiameterTouchOffInput,
                PartProbeTravelInput,
                PartProbeFeedInput,
                PartProbeFineFeedInput,
                PartProbeRetractInput,
                PartProbeTipDiameterInput,
                PartProbeZOffsetInput,
                PartProbeMoveFeedInput,
                PartProbeSetZeroAfterProbe,
                PartProbeCornerXDirection,
                PartProbeCornerYDirection,
                StraightEdgeDirection,
                StraightEdgeTouchCountInput,
                StraightEdgeSpacingInput,
                StraightEdgePullOffInput,
                StraightEdgeZLiftInput,
                SurfaceGridXCountInput,
                SurfaceGridYCountInput,
                SurfaceGridXSpacingInput,
                SurfaceGridYSpacingInput,
                ReferencePlateXInput,
                ReferencePlateYInput,
                ToolSetterXInput,
                ToolSetterYInput,
                ToolSetterProbeZInput,
                ToolSetterOffsetInput,
                UseToolSetter));
        }
        catch (Exception exception)
        {
            AddLog($"Machine settings save failed: {exception.Message}");
        }
    }

    private void LoadPersistedToolOffsets()
    {
        try
        {
            var storedEntries = ToolOffsetStorage.Load();
            if (storedEntries.Count == 0)
            {
                return;
            }

            _suppressToolOffsetPersistence = true;

            foreach (var storedEntry in storedEntries)
            {
                var (entry, _) = AddOrGetToolOffsetEntry(storedEntry.ToolNumber);
                entry.XOffsetInput = storedEntry.XOffsetInput;
                entry.ZOffsetInput = storedEntry.ZOffsetInput;
            }

            AddLog($"Loaded {storedEntries.Count} persisted tool offset entr{(storedEntries.Count == 1 ? "y" : "ies")}.");
        }
        catch (Exception exception)
        {
            AddLog($"Tool offset load failed: {exception.Message}");
        }
        finally
        {
            _suppressToolOffsetPersistence = false;
        }
    }

    private void EnsureMasterToolEntry()
    {
        var (masterEntry, created) = AddOrGetToolOffsetEntry(0);
        if (masterEntry.XOffsetInput != "0")
        {
            masterEntry.XOffsetInput = "0";
        }

        if (masterEntry.ZOffsetInput != "0")
        {
            masterEntry.ZOffsetInput = "0";
        }

        if (created)
        {
            AddLog("Added master tool T0.");
        }
    }

    private void PersistToolOffsets()
    {
        if (_suppressToolOffsetPersistence)
        {
            return;
        }

        try
        {
            ToolOffsetStorage.Save(ToolOffsets.Select(entry =>
                new ToolOffsetStorageEntry(entry.ToolNumber, entry.XOffsetInput, entry.ZOffsetInput)));
        }
        catch (Exception exception)
        {
            AddLog($"Tool offset save failed: {exception.Message}");
        }
    }

    private void RegisterToolOffsetEntry(ToolOffsetEntryViewModel toolOffsetEntry)
    {
        toolOffsetEntry.PropertyChanged += OnToolOffsetEntryPropertyChanged;
    }

    private void UnregisterToolOffsetEntry(ToolOffsetEntryViewModel toolOffsetEntry)
    {
        toolOffsetEntry.PropertyChanged -= OnToolOffsetEntryPropertyChanged;
    }

    private void OnToolOffsetEntryPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(ToolOffsetEntryViewModel.XOffsetInput) or nameof(ToolOffsetEntryViewModel.ZOffsetInput))
        {
            PersistToolOffsets();
            OnPropertyChanged(nameof(MasterRelativeOffsetText));
        }
    }

    private void CaptureToolChangePosition()
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before capturing the tool change X/Y position.");
            return;
        }

        ToolChangeXInput = MachineX.ToString("0.###", CultureInfo.InvariantCulture);
        ToolChangeYInput = MachineY.ToString("0.###", CultureInfo.InvariantCulture);
        AddLog($"Captured tool change position from current machine X/Y: X {MachineX:0.###}, Y {MachineY:0.###}.");
    }

    private void CaptureReferencePlatePosition()
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before capturing the reference plate X/Y position.");
            return;
        }

        ReferencePlateXInput = MachineX.ToString("0.###", CultureInfo.InvariantCulture);
        ReferencePlateYInput = MachineY.ToString("0.###", CultureInfo.InvariantCulture);
        AddLog($"Captured reference plate position from current machine X/Y: X {MachineX:0.###}, Y {MachineY:0.###}.");
    }

    private void CaptureProbeStartZ()
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before capturing the probe start Z position.");
            return;
        }

        ProbeStartZInput = MachineZ.ToString("0.###", CultureInfo.InvariantCulture);
        AddLog($"Captured probe start Z from current machine Z: Z {MachineZ:0.###}.");
    }

    private void CaptureToolSetterPosition()
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before capturing the tool setter position.");
            return;
        }

        ToolSetterXInput = MachineX.ToString("0.###", CultureInfo.InvariantCulture);
        ToolSetterYInput = MachineY.ToString("0.###", CultureInfo.InvariantCulture);
        ToolSetterProbeZInput = MachineZ.ToString("0.###", CultureInfo.InvariantCulture);
        AddLog($"Captured tool setter X/Y/probe Z from current machine position: X {MachineX:0.###}, Y {MachineY:0.###}, Z {MachineZ:0.###}.");
    }

    private void CaptureToolSetterProbeZ()
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before capturing the tool setter probe Z position.");
            return;
        }

        ToolSetterProbeZInput = MachineZ.ToString("0.###", CultureInfo.InvariantCulture);
        AddLog($"Captured tool setter probe start Z from current machine Z: Z {MachineZ:0.###}.");
    }

    private async Task GoToToolChangeAsync()
    {
        if (!TryGetMillToolChangeSettings(out var toolChangeX, out var toolChangeY, out var safeZ))
        {
            return;
        }

        try
        {
            AddLog($"Moving to safe Z {safeZ:0.###} mm and tool change X/Y {toolChangeX:0.###}, {toolChangeY:0.###}.");
            await MoveToMillToolChangeAsync(toolChangeX, toolChangeY, safeZ);
        }
        catch (Exception exception)
        {
            ShowOperationError("Go to tool change failed", exception);
        }
    }

    private async Task GoSafeZAsync()
    {
        if (!TryParseDouble(ToolChangeSafeZInput, "safe Z", out var safeZ))
        {
            return;
        }

        try
        {
            AddLog($"Moving to safe Z {safeZ:0.###} mm.");
            await _grblClient.MoveToMachineAsync(z: safeZ);
        }
        catch (Exception exception)
        {
            ShowOperationError("Go safe Z failed", exception);
        }
    }

    private async Task GoToXYZeroAsync()
    {
        if (!TryGetMillPlanarFeedRate(out var feedRate))
        {
            return;
        }

        try
        {
            await EnsureSelectedWorkCoordinateSystemActiveAsync();
            await _grblClient.MoveToAsync(x: 0, y: 0, feedRateMillimetersPerMinute: feedRate);
            AddLog($"Moving to work X 0 / Y 0 at F{feedRate:0.###}.");
        }
        catch (Exception exception)
        {
            ShowOperationError("Go to X/Y zero failed", exception);
        }
    }

    private void AddTool()
    {
        if (!TryParseToolNumber(NewToolNumberInput, out var toolNumber))
        {
            ShowValidationError("Enter a non-negative tool number.");
            return;
        }

        var (entry, created) = AddOrGetToolOffsetEntry(toolNumber);
        if (!created)
        {
            AddLog($"Tool T{toolNumber} already exists in the offset list.");
        }
        else
        {
            AddLog($"Added tool T{toolNumber} to the offset list.");
        }

        NewToolNumberInput = string.Empty;
    }

    private void LoadProgram()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "G-code files|*.nc;*.tap;*.gcode;*.txt|All files|*.*",
            Title = "Load G-code program"
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _loadedProgram = GCodeParser.ParseFile(openFileDialog.FileName, MachineMode);
            ToolPathSegments = _loadedProgram.Segments;
            ProgramPath = _loadedProgram.FilePath;
            ExecutedProgramLines = 0;
            ProgramProgressPercent = 0;
            OnPropertyChanged(nameof(ProgramSummaryText));
            OnPropertyChanged(nameof(ProgramProgressText));
            if (IsLatheMode)
            {
                MergeToolOffsetsFromProgram(_loadedProgram);
            }
            AddLog($"Loaded program: {_loadedProgram.DisplayName}");
            RefreshCommandStates();
        }
        catch (Exception exception)
        {
            ShowOperationError("Program load failed", exception);
        }
    }

    private void MergeToolOffsetsFromProgram(GCodeProgram program)
    {
        var addedTools = 0;
        foreach (var toolNumber in program.ToolNumbers)
        {
            var (_, created) = AddOrGetToolOffsetEntry(toolNumber);
            if (created)
            {
                addedTools++;
            }
        }

        if (addedTools > 0)
        {
            AddLog($"Added {addedTools} tool offset entr{(addedTools == 1 ? "y" : "ies")} from program.");
        }
    }

    private async Task StartProgramAsync()
    {
        if (_loadedProgram is null)
        {
            ShowValidationError("Load a G-code file before starting playback.");
            return;
        }

        _programCancellation = new CancellationTokenSource();
        IsProgramRunning = true;
        IsProgramPaused = false;
        ExecutedProgramLines = 0;
        ProgramProgressPercent = 0;
        await EnsureSelectedWorkCoordinateSystemActiveAsync();
        AddLog($"Starting program: {_loadedProgram.DisplayName}");

        try
        {
            var progress = new Progress<(int completedLines, int totalLines)>(update =>
            {
                ExecutedProgramLines = update.completedLines;
                ProgramProgressPercent = update.totalLines == 0
                    ? 0
                    : (double)update.completedLines / update.totalLines * 100d;
            });

            await StreamProgramBlocksAsync(_loadedProgram.Blocks, progress, _programCancellation.Token);
            AddLog("Program complete.");
        }
        catch (OperationCanceledException)
        {
            AddLog("Program playback stopped.");
        }
        catch (Exception exception)
        {
            ShowOperationError("Program playback failed", exception);
        }
        finally
        {
            _programCancellation?.Dispose();
            _programCancellation = null;
            IsProgramRunning = false;
            IsProgramPaused = false;
            _lastAppliedWorkCoordinateSystem = string.Empty;
            OnPropertyChanged(nameof(ProgramSummaryText));
        }
    }

    private async Task StreamProgramBlocksAsync(
        IReadOnlyList<GCodeBlock> blocks,
        IProgress<(int completedLines, int totalLines)>? progress,
        CancellationToken cancellationToken)
    {
        var promptedToolBlockIndexes = new HashSet<int>();
        var totalCommands = blocks.Count(block => block.ShouldSendToController);
        var completedCommands = 0;

        for (var index = 0; index < blocks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = blocks[index];

            if (!block.IsPauseCommand &&
                block.ToolNumber.HasValue &&
                !promptedToolBlockIndexes.Contains(index))
            {
                await PromptForToolOffsetsAsync(block.ToolNumber.Value, cancellationToken);
                promptedToolBlockIndexes.Add(index);
            }

            await WaitForProgramResumeAsync(cancellationToken);

            if (block.ShouldSendToController)
            {
                await _grblClient.SendCommandAsync(block.CommandLine, cancellationToken);
                completedCommands++;
                progress?.Report((completedCommands, totalCommands));
            }

            if (!block.IsPauseCommand)
            {
                continue;
            }

            if (!IsProgramPaused)
            {
                IsProgramPaused = true;
                AddLog("Program paused by G-code.");
            }

            if (block.ToolNumber.HasValue && !promptedToolBlockIndexes.Contains(index))
            {
                await PromptForToolOffsetsAsync(block.ToolNumber.Value, cancellationToken);
                promptedToolBlockIndexes.Add(index);
            }
            else if (index + 1 < blocks.Count &&
                     blocks[index + 1].ToolNumber.HasValue &&
                     !promptedToolBlockIndexes.Contains(index + 1))
            {
                await PromptForToolOffsetsAsync(blocks[index + 1].ToolNumber!.Value, cancellationToken);
                promptedToolBlockIndexes.Add(index + 1);
            }

            await WaitForProgramResumeAsync(cancellationToken);
        }
    }

    private async Task PauseResumeProgramAsync()
    {
        try
        {
            if (IsProgramPaused)
            {
                await _grblClient.ResumeAsync();
                IsProgramPaused = false;
                AddLog("Program resumed.");
            }
            else
            {
                await _grblClient.FeedHoldAsync();
                IsProgramPaused = true;
                AddLog("Program paused.");
            }
        }
        catch (Exception exception)
        {
            ShowOperationError("Pause/resume failed", exception);
        }
    }

    private async Task StopProgramAsync()
    {
        await SendSoftResetAsync("Program playback stopped. Soft reset sent to GRBL.", "Stop failed");
    }

    private async Task SoftResetAsync()
    {
        await SendSoftResetAsync("Soft reset sent to GRBL.", "Soft reset failed");
    }

    private async Task UnlockAsync()
    {
        try
        {
            await _grblClient.UnlockAsync();
            AddLog("GRBL unlock ($X) sent.");
        }
        catch (Exception exception)
        {
            ShowOperationError("Unlock failed", exception);
        }
    }

    private bool CanSendTerminalCommand()
    {
        return IsConnected && !string.IsNullOrWhiteSpace(TerminalCommandInput);
    }

    private async Task SendTerminalCommandAsync()
    {
        var commandText = TerminalCommandInput.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return;
        }

        try
        {
            AddLog($"> {commandText}");
            var response = await _grblClient.SendTerminalCommandAsync(commandText);
            if (!string.IsNullOrWhiteSpace(response))
            {
                AddLog($"< {response}");
            }

            TerminalCommandInput = string.Empty;
        }
        catch (Exception exception)
        {
            ShowOperationError("Terminal command failed", exception);
        }
    }

    private async Task ReadControllerSettingsAsync(bool showErrors = true)
    {
        try
        {
            AddLog("Reading controller $$ settings.");
            var settings = await _grblClient.ReadSettingsAsync();
            if (settings.Count == 0)
            {
                AddLog("Controller settings read completed, but no $ settings were parsed.");
                return;
            }

            ApplyControllerSettings(settings);
            AddLog($"Read {settings.Count} controller setting(s).");
        }
        catch (Exception exception)
        {
            if (showErrors)
            {
                ShowOperationError("Read settings failed", exception);
            }
            else
            {
                AddLog($"Read settings failed: {exception.Message}");
            }
        }
    }

    private void ApplyControllerSettings(IReadOnlyDictionary<int, double> settings)
    {
        var previousSuppress = _suppressMachineSettingsPersistence;
        _suppressMachineSettingsPersistence = true;

        try
        {
            if (settings.TryGetValue(30, out var maxSpindleSpeed) && maxSpindleSpeed > 0)
            {
                var spindleMax = Math.Max(1, (int)Math.Round(maxSpindleSpeed));
                if (IsLatheMode)
                {
                    _latheSpindleMaxSpeed = spindleMax;
                    LatheSpindleMaxSpeedInput = spindleMax.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    _millSpindleMaxSpeed = spindleMax;
                    MillSpindleMaxSpeedInput = spindleMax.ToString(CultureInfo.InvariantCulture);
                }

                SpindleMaxSpeed = spindleMax;
                SpindleMaxSpeedInput = spindleMax.ToString(CultureInfo.InvariantCulture);
            }

            if (settings.TryGetValue(110, out var xMaxRate) && xMaxRate > 0)
            {
                XJogFeedInput = FormatControllerSetting(xMaxRate);
            }

            if (IsMillMode && settings.TryGetValue(111, out var yMaxRate) && yMaxRate > 0)
            {
                YJogFeedInput = FormatControllerSetting(yMaxRate);
            }

            if (settings.TryGetValue(112, out var zMaxRate) && zMaxRate > 0)
            {
                ZJogFeedInput = FormatControllerSetting(zMaxRate);
            }

            if (IsMillMode && settings.TryGetValue(113, out var aMaxRate) && aMaxRate > 0)
            {
                AJogFeedInput = FormatControllerSetting(aMaxRate);
            }

            if (IsMillMode && settings.TryGetValue(114, out var bMaxRate) && bMaxRate > 0)
            {
                BJogFeedInput = FormatControllerSetting(bMaxRate);
            }

            if (settings.TryGetValue(6, out var probeInvert))
            {
                SetProbePinModeFromController(Math.Abs(probeInvert) >= 0.5d);
            }
        }
        finally
        {
            _suppressMachineSettingsPersistence = previousSuppress;
        }

        PersistMachineSettings();
    }

    private async Task<bool> SetProbePinInvertAsync(bool normallyClosed)
    {
        try
        {
            var settingValue = normallyClosed ? 1 : 0;
            await _grblClient.SetSettingAsync(6, settingValue);
            AddLog(normallyClosed
                ? "Probe pin mode set to NC ($6=1)."
                : "Probe pin mode set to NO ($6=0).");
            return true;
        }
        catch (Exception exception)
        {
            SetProbePinModeFromController(!normallyClosed);
            ShowOperationError("Probe pin mode update failed", exception);
            return false;
        }
    }

    private void SetProbePinModeFromController(bool normallyClosed)
    {
        _suppressProbePinModeWrite = true;
        try
        {
            IsProbeNormallyClosed = normallyClosed;
        }
        finally
        {
            _suppressProbePinModeWrite = false;
        }
    }

    private void SaveSettings()
    {
        if (!TryParsePositiveInt(LatheSpindleMaxSpeedInput, out var latheSpindleMaxSpeed))
        {
            ShowValidationError("Enter a positive whole number for lathe spindle max speed.");
            return;
        }

        if (!TryParsePositiveInt(MillSpindleMaxSpeedInput, out var millSpindleMaxSpeed))
        {
            ShowValidationError("Enter a positive whole number for mill spindle max speed.");
            return;
        }

        _latheSpindleMaxSpeed = latheSpindleMaxSpeed;
        _millSpindleMaxSpeed = millSpindleMaxSpeed;

        var activeSpindleMax = IsLatheMode ? _latheSpindleMaxSpeed : _millSpindleMaxSpeed;
        if (SpindleMaxSpeed != activeSpindleMax)
        {
            SpindleMaxSpeed = activeSpindleMax;
            SpindleMaxSpeedInput = activeSpindleMax.ToString(CultureInfo.InvariantCulture);
        }

        PersistMachineSettings();
        if (IsLatheMode)
        {
            PersistToolOffsets();
        }

        AddLog("Settings saved.");
    }

    private async Task SendSoftResetAsync(string successMessage, string errorCaption)
    {
        try
        {
            _programCancellation?.Cancel();
            _feedOverrideAdjustmentCancellation?.Cancel();
            await _grblClient.SoftResetAsync();
            IsProgramRunning = false;
            IsProgramPaused = false;
            _lastAppliedWorkCoordinateSystem = string.Empty;
            UpdateFeedOverrideFromController(100);
            ControllerState = "Resetting";
            AddLog(successMessage);
        }
        catch (Exception exception)
        {
            ShowOperationError(errorCaption, exception);
        }
    }

    private async Task WaitForProgramResumeAsync(CancellationToken cancellationToken)
    {
        while (IsProgramPaused)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(75, cancellationToken);
        }
    }

    private async Task PromptForToolOffsetsAsync(int toolNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsLatheMode)
        {
            return;
        }

        var toolEntry = ToolOffsets.FirstOrDefault(entry => entry.ToolNumber == toolNumber);
        if (toolEntry is null)
        {
            var missingToolResult = MessageBox.Show(
                $"Tool T{toolNumber} is referenced in the program, but no stored offsets were found. Continue without applying offsets?",
                "Tool Offsets",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (missingToolResult != MessageBoxResult.Yes)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            AddLog($"Continuing without stored offsets for T{toolNumber}.");
            return;
        }

        var result = MessageBox.Show(
            $"Tool T{toolNumber} was requested by the program. Apply its stored X/Z offsets now?",
            "Tool Offsets",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (result == MessageBoxResult.Yes)
        {
            await ApplyToolOffsetAsync(toolEntry, $"Applied stored offsets for T{toolNumber}.");
        }
        else
        {
            AddLog($"Skipped stored offsets for T{toolNumber}.");
        }
    }

    private (ToolOffsetEntryViewModel entry, bool created) AddOrGetToolOffsetEntry(int toolNumber)
    {
        var existingEntry = ToolOffsets.FirstOrDefault(entry => entry.ToolNumber == toolNumber);
        if (existingEntry is not null)
        {
            return (existingEntry, false);
        }

        var newEntry = new ToolOffsetEntryViewModel(toolNumber, CaptureToolOffsetFromCurrentPosition, ApplyToolOffsetAsync, DeleteToolOffsetEntry);
        var insertIndex = 0;
        while (insertIndex < ToolOffsets.Count && ToolOffsets[insertIndex].ToolNumber < toolNumber)
        {
            insertIndex++;
        }

        RegisterToolOffsetEntry(newEntry);
        ToolOffsets.Insert(insertIndex, newEntry);
        PersistToolOffsets();
        return (newEntry, true);
    }

    private void CaptureToolOffsetFromCurrentPosition(ToolOffsetEntryViewModel toolEntry)
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before capturing a tool offset.");
            return;
        }

        if (toolEntry.ToolNumber == 0)
        {
            toolEntry.CaptureOffsets(0, 0);
            AddLog("T0 remains the master tool at X 0 / Z 0.");
            return;
        }

        toolEntry.CaptureOffsets(-WorkX, -WorkZ);
        AddLog($"Captured T{toolEntry.ToolNumber} tool-tip offset from T0.");
    }

    private void DeleteToolOffsetEntry(ToolOffsetEntryViewModel toolEntry)
    {
        if (toolEntry.ToolNumber == 0)
        {
            ShowValidationError("T0 is the master tool and cannot be removed.");
            return;
        }

        if (toolEntry.ToolNumber == ActiveToolNumber)
        {
            ShowValidationError($"Use T0 or another tool before deleting active tool T{toolEntry.ToolNumber}.");
            return;
        }

        UnregisterToolOffsetEntry(toolEntry);
        ToolOffsets.Remove(toolEntry);
        PersistToolOffsets();
        AddLog($"Deleted tool T{toolEntry.ToolNumber} from the offset list.");
    }

    private void ExecuteKeyboardJog(bool positiveDirection)
    {
        var command = KeyboardJogAxis switch
        {
            'X' => positiveDirection ? JogXPositiveCommand : JogXNegativeCommand,
            'Y' => positiveDirection ? JogYPositiveCommand : JogYNegativeCommand,
            'Z' => positiveDirection ? JogZPositiveCommand : JogZNegativeCommand,
            'A' => positiveDirection ? JogAPositiveCommand : JogANegativeCommand,
            'B' => positiveDirection ? JogBPositiveCommand : JogBNegativeCommand,
            _ => positiveDirection ? JogXPositiveCommand : JogXNegativeCommand
        };

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void AdjustKeyboardStep(int direction)
    {
        switch (KeyboardJogAxis)
        {
            case 'X':
                SelectedXJogStep = GetAdjacentStep(XJogSteps, SelectedXJogStep, direction);
                break;
            case 'Y':
                SelectedYJogStep = GetAdjacentStep(LinearJogSteps, SelectedYJogStep, direction);
                break;
            case 'Z':
                SelectedZJogStep = GetAdjacentStep(ZJogSteps, SelectedZJogStep, direction);
                break;
            case 'A':
                SelectedAJogStep = GetAdjacentStep(RotaryJogSteps, SelectedAJogStep, direction);
                break;
            case 'B':
                SelectedBJogStep = GetAdjacentStep(RotaryJogSteps, SelectedBJogStep, direction);
                break;
        }
    }

    private void AdjustKeyboardFeed(double delta)
    {
        switch (KeyboardJogAxis)
        {
            case 'X':
                XJogFeedInput = AdjustFeedInput(XJogFeedInput, delta);
                break;
            case 'Y':
                YJogFeedInput = AdjustFeedInput(YJogFeedInput, delta);
                break;
            case 'Z':
                ZJogFeedInput = AdjustFeedInput(ZJogFeedInput, delta);
                break;
            case 'A':
                AJogFeedInput = AdjustFeedInput(AJogFeedInput, delta);
                break;
            case 'B':
                BJogFeedInput = AdjustFeedInput(BJogFeedInput, delta);
                break;
        }
    }

    private double GetActiveKeyboardStep()
    {
        return KeyboardJogAxis switch
        {
            'X' => SelectedXJogStep,
            'Y' => SelectedYJogStep,
            'Z' => SelectedZJogStep,
            'A' => SelectedAJogStep,
            'B' => SelectedBJogStep,
            _ => SelectedXJogStep
        };
    }

    private string GetActiveKeyboardFeedText()
    {
        return KeyboardJogAxis switch
        {
            'X' => XJogFeedInput,
            'Y' => YJogFeedInput,
            'Z' => ZJogFeedInput,
            'A' => AJogFeedInput,
            'B' => BJogFeedInput,
            _ => XJogFeedInput
        };
    }

    private char GetNextKeyboardAxis()
    {
        var supportedAxes = IsMillMode
            ? new[] { 'X', 'Y', 'Z', 'A', 'B' }
            : new[] { 'X', 'Z' };
        var currentIndex = Array.IndexOf(supportedAxes, KeyboardJogAxis);
        if (currentIndex < 0)
        {
            return supportedAxes[0];
        }

        return supportedAxes[(currentIndex + 1) % supportedAxes.Length];
    }

    private static double GetAdjacentStep(IReadOnlyList<double> steps, double currentStep, int direction)
    {
        if (steps.Count == 0)
        {
            return currentStep;
        }

        var nearestIndex = 0;
        var nearestDistance = double.MaxValue;
        for (var index = 0; index < steps.Count; index++)
        {
            var distance = Math.Abs(steps[index] - currentStep);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = index;
            }
        }

        var targetIndex = Math.Clamp(nearestIndex + direction, 0, steps.Count - 1);
        return steps[targetIndex];
    }

    private static string AdjustFeedInput(string currentInput, double delta)
    {
        if (!TryParseFlexibleDouble(currentInput, out var currentFeed) || currentFeed <= 0)
        {
            currentFeed = 10;
        }

        return Math.Max(1, currentFeed + delta).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private Task ApplyToolOffsetAsync(ToolOffsetEntryViewModel toolEntry)
    {
        return ApplyToolOffsetAsync(toolEntry, $"Applied offsets for T{toolEntry.ToolNumber}.");
    }

    private async Task ApplyToolOffsetAsync(ToolOffsetEntryViewModel toolEntry, string successMessage)
    {
        if (!IsConnected)
        {
            ShowValidationError("Connect to the controller before applying a tool offset.");
            return;
        }

        if (!TryGetStoredToolOffsets(toolEntry.ToolNumber, out var targetToolX, out var targetToolZ))
        {
            ShowValidationError($"Enter valid X and Z offsets for T{toolEntry.ToolNumber}.");
            return;
        }

        if (!TryGetStoredToolOffsets(ActiveToolNumber, out var currentToolX, out var currentToolZ))
        {
            ShowValidationError($"Enter valid X and Z offsets for the active tool T{ActiveToolNumber} before switching tools.");
            return;
        }

        try
        {
            var desiredWorkX = WorkX + (targetToolX - currentToolX);
            var desiredWorkZ = WorkZ + (targetToolZ - currentToolZ);
            await _grblClient.SetWorkCoordinateOffsetAsync(
                x: desiredWorkX,
                z: desiredWorkZ,
                workCoordinateSystem: SelectedWorkCoordinateSystem);
            await EnsureSelectedWorkCoordinateSystemActiveAsync();
            WorkX = desiredWorkX;
            WorkZ = desiredWorkZ;
            ActiveToolNumber = toolEntry.ToolNumber;
            AddLog(successMessage);
        }
        catch (Exception exception)
        {
            ShowOperationError("Tool offset apply failed", exception);
        }
    }

    private bool TryGetStoredToolOffsets(int toolNumber, out double xOffset, out double zOffset)
    {
        if (toolNumber == 0)
        {
            xOffset = 0;
            zOffset = 0;
            return true;
        }

        var toolEntry = ToolOffsets.FirstOrDefault(entry => entry.ToolNumber == toolNumber);
        if (toolEntry is not null && toolEntry.TryGetOffsets(out xOffset, out zOffset))
        {
            return true;
        }

        xOffset = 0;
        zOffset = 0;
        return false;
    }

    private void OnGrblStatusReceived(object? sender, GrblStatus status)
    {
        _ = _dispatcher.BeginInvoke(() =>
        {
            if (status.MachineX.HasValue)
            {
                MachineX = status.MachineX.Value;
            }

            if (status.MachineY.HasValue)
            {
                MachineY = status.MachineY.Value;
            }

            if (status.MachineZ.HasValue)
            {
                MachineZ = status.MachineZ.Value;
            }

            if (status.MachineA.HasValue)
            {
                MachineA = status.MachineA.Value;
            }

            if (status.MachineB.HasValue)
            {
                MachineB = status.MachineB.Value;
            }

            if (status.WorkX.HasValue)
            {
                WorkX = status.WorkX.Value;
                WorkXInput = status.WorkX.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (status.WorkY.HasValue)
            {
                WorkY = status.WorkY.Value;
                WorkYInput = status.WorkY.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (status.WorkZ.HasValue)
            {
                WorkZ = status.WorkZ.Value;
                WorkZInput = status.WorkZ.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (status.WorkA.HasValue)
            {
                WorkA = status.WorkA.Value;
                WorkAInput = status.WorkA.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (status.WorkB.HasValue)
            {
                WorkB = status.WorkB.Value;
                WorkBInput = status.WorkB.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (_partProbeMeasureA is not null || _partProbeMeasureB is not null || _lastMeasuredPartEdge is not null)
            {
                OnPropertyChanged(nameof(PartProbeMeasurementText));
            }

            XLimitPinHigh = status.XLimitPinHigh;
            YLimitPinHigh = status.YLimitPinHigh;
            ZLimitPinHigh = status.ZLimitPinHigh;
            ProbePinHigh = status.ProbePinHigh;

            if (status.FeedOverridePercent.HasValue)
            {
                UpdateFeedOverrideFromController(status.FeedOverridePercent.Value);
            }

            if (IsProgramRunning)
            {
                var controllerPaused = IsControllerPausedState(status.State);
                if (IsProgramPaused != controllerPaused)
                {
                    IsProgramPaused = controllerPaused;
                }
            }

            ControllerState = status.State;
        }, DispatcherPriority.Background);
    }

    private void OnGrblMessageReceived(object? sender, string message)
    {
        _ = _dispatcher.BeginInvoke(() =>
        {
            if (TryParseProbeTouch(message, out var probeTouch))
            {
                _lastProbeTouch = probeTouch;
                _lastProbeSequence++;
            }

            AddLog(message);
        }, DispatcherPriority.Background);
    }

    private static bool TryParseProbeTouch(string message, out ProbeTouch probeTouch)
    {
        probeTouch = default!;

        if (string.IsNullOrWhiteSpace(message) ||
            !message.StartsWith("[PRB:", StringComparison.OrdinalIgnoreCase) ||
            !message.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        var payload = message[5..^1];
        var sections = payload.Split(':');
        if (sections.Length != 2 || !string.Equals(sections[1], "1", StringComparison.Ordinal))
        {
            return false;
        }

        var values = sections[0].Split(',');
        if (values.Length < 3)
        {
            return false;
        }

        var parsed = new double[5];
        for (var index = 0; index < parsed.Length; index++)
        {
            if (index >= values.Length)
            {
                parsed[index] = 0;
                continue;
            }

            if (!TryParseFlexibleDouble(values[index], out parsed[index]))
            {
                return false;
            }
        }

        probeTouch = new ProbeTouch(parsed[0], parsed[1], parsed[2], parsed[3], parsed[4]);
        return true;
    }

    private void AddLog(string message)
    {
        var timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ControllerLog.Insert(0, timestampedMessage);

        while (ControllerLog.Count > 200)
        {
            ControllerLog.RemoveAt(ControllerLog.Count - 1);
        }
    }

    private void ShowValidationError(string message)
    {
        AddLog(message);
        MessageBox.Show(message, "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowOperationError(string caption, Exception exception)
    {
        var message = $"{caption}: {exception.Message}";
        AddLog(message);
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void RefreshCommandStates()
    {
        RefreshPortsCommand.RaiseCanExecuteChanged();
        AddToolCommand.RaiseCanExecuteChanged();
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        ZeroXCommand.RaiseCanExecuteChanged();
        ZeroYCommand.RaiseCanExecuteChanged();
        ZeroZCommand.RaiseCanExecuteChanged();
        ZeroACommand.RaiseCanExecuteChanged();
        ZeroBCommand.RaiseCanExecuteChanged();
        ZeroAllCommand.RaiseCanExecuteChanged();
        HomeCommand.RaiseCanExecuteChanged();
        SoftResetCommand.RaiseCanExecuteChanged();
        UnlockCommand.RaiseCanExecuteChanged();
        SendTerminalCommandCommand.RaiseCanExecuteChanged();
        ReadControllerSettingsCommand.RaiseCanExecuteChanged();
        SetSpindleMaxSpeedCommand.RaiseCanExecuteChanged();
        ApplySpindleSpeedCommand.RaiseCanExecuteChanged();
        StopSpindleCommand.RaiseCanExecuteChanged();
        SetWorkXCommand.RaiseCanExecuteChanged();
        SetWorkYCommand.RaiseCanExecuteChanged();
        SetWorkZCommand.RaiseCanExecuteChanged();
        SetWorkACommand.RaiseCanExecuteChanged();
        SetWorkBCommand.RaiseCanExecuteChanged();
        SetXFromDiameterCommand.RaiseCanExecuteChanged();
        GoToXCommand.RaiseCanExecuteChanged();
        GoToYCommand.RaiseCanExecuteChanged();
        GoToZCommand.RaiseCanExecuteChanged();
        GoToRadiusPlusOneCommand.RaiseCanExecuteChanged();
        JogXPositiveCommand.RaiseCanExecuteChanged();
        JogXNegativeCommand.RaiseCanExecuteChanged();
        JogYPositiveCommand.RaiseCanExecuteChanged();
        JogYNegativeCommand.RaiseCanExecuteChanged();
        JogXYNorthwestCommand.RaiseCanExecuteChanged();
        JogXYNorthCommand.RaiseCanExecuteChanged();
        JogXYNortheastCommand.RaiseCanExecuteChanged();
        JogXYWestCommand.RaiseCanExecuteChanged();
        JogXYEastCommand.RaiseCanExecuteChanged();
        JogXYSouthwestCommand.RaiseCanExecuteChanged();
        JogXYSouthCommand.RaiseCanExecuteChanged();
        JogXYSoutheastCommand.RaiseCanExecuteChanged();
        JogZPositiveCommand.RaiseCanExecuteChanged();
        JogZNegativeCommand.RaiseCanExecuteChanged();
        JogAPositiveCommand.RaiseCanExecuteChanged();
        JogANegativeCommand.RaiseCanExecuteChanged();
        JogBPositiveCommand.RaiseCanExecuteChanged();
        JogBNegativeCommand.RaiseCanExecuteChanged();
        CaptureToolChangePositionCommand.RaiseCanExecuteChanged();
        CaptureReferencePlatePositionCommand.RaiseCanExecuteChanged();
        CaptureProbeStartZCommand.RaiseCanExecuteChanged();
        CaptureToolSetterPositionCommand.RaiseCanExecuteChanged();
        CaptureToolSetterProbeZCommand.RaiseCanExecuteChanged();
        GoToToolChangeCommand.RaiseCanExecuteChanged();
        GoSafeZCommand.RaiseCanExecuteChanged();
        GoToXYZeroCommand.RaiseCanExecuteChanged();
        CalibrateToolProbePlateCommand.RaiseCanExecuteChanged();
        CalibrateToolSetterOffsetCommand.RaiseCanExecuteChanged();
        RunToolProbeCommand.RaiseCanExecuteChanged();
        ProbeXNegativeEdgeCommand.RaiseCanExecuteChanged();
        ProbeXPositiveEdgeCommand.RaiseCanExecuteChanged();
        ProbeYNegativeEdgeCommand.RaiseCanExecuteChanged();
        ProbeYPositiveEdgeCommand.RaiseCanExecuteChanged();
        ProbeZTouchCommand.RaiseCanExecuteChanged();
        ProbeXYCornerCommand.RaiseCanExecuteChanged();
        ProbeHoleCenterCommand.RaiseCanExecuteChanged();
        CalculateOutsideCylinderCommand.RaiseCanExecuteChanged();
        SetPartProbeMeasureACommand.RaiseCanExecuteChanged();
        SetPartProbeMeasureBCommand.RaiseCanExecuteChanged();
        SetPartProbeMeasurementCenterZeroCommand.RaiseCanExecuteChanged();
        RunStraightEdgeProbeCommand.RaiseCanExecuteChanged();
        RunSurfaceGridProbeCommand.RaiseCanExecuteChanged();
        LoadProgramCommand.RaiseCanExecuteChanged();
        StartProgramCommand.RaiseCanExecuteChanged();
        PauseResumeProgramCommand.RaiseCanExecuteChanged();
        StopProgramCommand.RaiseCanExecuteChanged();
    }

    private bool CanAdjustManualSpindle()
    {
        return CanManualSpindleControl;
    }

    private void ScheduleFeedOverrideUpdate()
    {
        if (!IsConnected)
        {
            return;
        }

        _feedOverrideAdjustmentCancellation?.Cancel();
        _feedOverrideAdjustmentCancellation?.Dispose();

        _feedOverrideAdjustmentCancellation = new CancellationTokenSource();
        var cancellationToken = _feedOverrideAdjustmentCancellation.Token;
        var targetPercent = FeedOverridePercent;

        _ = ApplyFeedOverrideAsync(targetPercent, cancellationToken);
    }

    private async Task ApplyFeedOverrideAsync(int targetPercent, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            await _grblClient.SetFeedOverrideAsync(targetPercent, _lastKnownFeedOverridePercent, cancellationToken);
            _lastKnownFeedOverridePercent = targetPercent;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowOperationError("Feed override update failed", exception);
        }
    }

    private void UpdateFeedOverrideFromController(int overridePercent)
    {
        _lastKnownFeedOverridePercent = Math.Clamp(overridePercent, 25, 200);
        _isUpdatingFeedOverrideFromController = true;

        try
        {
            FeedOverridePercent = _lastKnownFeedOverridePercent;
        }
        finally
        {
            _isUpdatingFeedOverrideFromController = false;
        }
    }

    private static bool IsControllerPausedState(string controllerState)
    {
        return controllerState.StartsWith("Hold", StringComparison.OrdinalIgnoreCase) ||
               controllerState.StartsWith("Door", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProbeWorkTouch> ProbeTouchFromStartAsync(
        string axisLetter,
        int direction,
        double startX,
        double startY,
        double travel,
        double feed,
        double fineFeed,
        double retract)
    {
        var touch = await ExecutePartProbeTouchAsync(axisLetter, direction, travel, feed, fineFeed, retract);
        await MoveRelativeForAxisAsync(axisLetter, -direction * retract, feed);
        await _grblClient.MoveToAsync(x: startX, y: startY, feedRateMillimetersPerMinute: feed);
        return touch;
    }

    private async Task<ProbeWorkTouch> ExecutePartProbeTouchAsync(
        string axisLetter,
        int direction,
        double travel,
        double feed,
        double fineFeed,
        double retract)
    {
        var normalizedAxis = axisLetter.ToUpperInvariant();
        await WaitForProbePinStateAsync(
            expectedHigh: false,
            "Probe input is active before the probe cycle. Check the probe plate, wiring, or $6 NO/NC mode.");

        var initialProbeSequence = _lastProbeSequence;
        var startingMachine = GetCurrentMachineAxis(normalizedAxis);
        var startingWork = GetCurrentWorkAxis(normalizedAxis);
        var coarseTravel = Math.Abs(travel);
        if (normalizedAxis == "Z" &&
            direction < 0 &&
            !TryGetSafeDownwardZProbeTravel(coarseTravel, "part Z coarse probe", out coarseTravel))
        {
            throw new InvalidOperationException("Part Z probe stopped because there is not enough safe machine Z travel remaining.");
        }

        await _grblClient.ProbeAxisRelativeAsync(normalizedAxis, direction * coarseTravel, feed);
        var coarseTouch = await WaitForPartProbeTouchAsync(normalizedAxis, initialProbeSequence, startingMachine, startingWork);

        await MoveRelativeForAxisAsync(normalizedAxis, -direction * Math.Abs(retract), feed);
        await WaitForProbePinStateAsync(expectedHigh: false, "Probe input stayed active after pull-off. Increase pull-off or check probe wiring.");

        var fineProbeSequence = _lastProbeSequence;
        var fineStartingMachine = GetCurrentMachineAxis(normalizedAxis);
        var fineStartingWork = GetCurrentWorkAxis(normalizedAxis);
        var fineTravel = Math.Max(Math.Abs(retract) * 2d, 1d);
        if (normalizedAxis == "Z" &&
            direction < 0 &&
            !TryGetSafeDownwardZProbeTravel(fineTravel, "part Z fine probe", out fineTravel))
        {
            throw new InvalidOperationException("Part Z fine probe stopped because there is not enough safe machine Z travel remaining.");
        }

        await _grblClient.ProbeAxisRelativeAsync(normalizedAxis, direction * fineTravel, fineFeed);
        var fineTouch = await WaitForPartProbeTouchAsync(normalizedAxis, fineProbeSequence, fineStartingMachine, fineStartingWork);

        AddLog(
            $"Part probe {normalizedAxis}{FormatDirection(direction)} fine touch at " +
            $"{FormatAxisValue(normalizedAxis, GetWorkAxis(fineTouch, normalizedAxis))} " +
            $"(coarse {FormatAxisValue(normalizedAxis, GetWorkAxis(coarseTouch, normalizedAxis))}).");
        return fineTouch;
    }

    private async Task<ProbeWorkTouch> WaitForPartProbeTouchAsync(
        string axisLetter,
        int startingProbeSequence,
        double startingMachine,
        double startingWork)
    {
        const int maxAttempts = 40;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_lastProbeSequence > startingProbeSequence && _lastProbeTouch is not null)
            {
                return ConvertProbeTouch(_lastProbeTouch);
            }

            if ((ProbePinHigh || Math.Abs(GetCurrentMachineAxis(axisLetter) - startingMachine) > 0.0005d) &&
                Math.Abs(GetCurrentWorkAxis(axisLetter) - startingWork) > 0.0005d)
            {
                return SnapshotCurrentProbeTouch();
            }

            await Task.Delay(50);
        }

        AddLog("Probe finished before a fresh probe/status report arrived; using the latest reported position.");
        return SnapshotCurrentProbeTouch();
    }

    private async Task MoveRelativeForAxisAsync(string axisLetter, double distance, double feed)
    {
        var normalizedAxis = axisLetter.ToUpperInvariant();
        await _grblClient.MoveRelativeAsync(
            x: normalizedAxis == "X" ? distance : null,
            y: normalizedAxis == "Y" ? distance : null,
            z: normalizedAxis == "Z" ? distance : null,
            a: normalizedAxis == "A" ? distance : null,
            b: normalizedAxis == "B" ? distance : null,
            feedRateMillimetersPerMinute: feed);
    }

    private bool TryGetPartProbeSettings(
        out double travel,
        out double feed,
        out double fineFeed,
        out double retract,
        out double tipRadius,
        out double zOffset)
    {
        travel = 0;
        feed = 0;
        fineFeed = 0;
        retract = 0;
        tipRadius = 0;
        zOffset = 0;

        if (!TryParseDouble(PartProbeTravelInput, "part probe travel", out travel) ||
            !TryParseDouble(PartProbeFeedInput, "part probe feed", out feed) ||
            !TryParseDouble(PartProbeFineFeedInput, "part fine probe feed", out fineFeed) ||
            !TryParseDouble(PartProbeRetractInput, "part probe pull-off", out retract) ||
            !TryParseDouble(PartProbeTipDiameterInput, "probe tip diameter", out var tipDiameter) ||
            !TryParseDouble(PartProbeZOffsetInput, "part probe Z zero offset", out zOffset))
        {
            return false;
        }

        if (travel <= 0 || feed <= 0 || fineFeed <= 0 || retract <= 0 || tipDiameter < 0)
        {
            ShowValidationError("Enter positive part probe travel/feed/pull-off values and a non-negative probe tip diameter.");
            return false;
        }

        tipRadius = tipDiameter / 2d;
        return true;
    }

    private bool TryGetPartProbeMoveFeed(out double moveFeed)
    {
        if (!TryParseDouble(PartProbeMoveFeedInput, "part probe macro move feed", out moveFeed))
        {
            return false;
        }

        if (moveFeed <= 0)
        {
            ShowValidationError("Enter a positive part probe macro move feed.");
            return false;
        }

        return true;
    }

    private bool TryGetSafeDownwardZProbeTravel(double requestedTravel, string probeLabel, out double safeTravel)
    {
        safeTravel = Math.Abs(requestedTravel);
        var requestedProbeDistance = -safeTravel;
        if (!TryGetSafeZProbeDistance(requestedProbeDistance, probeLabel, out var safeProbeDistance))
        {
            return false;
        }

        safeTravel = Math.Abs(safeProbeDistance);
        return safeTravel > 0.001d;
    }

    private bool TryGetSafeZProbeDistance(double requestedProbeDistance, string probeLabel, out double safeProbeDistance)
    {
        safeProbeDistance = requestedProbeDistance;
        if (!TryGetZProbeSafetySettings(out var minimumMachineZ, out var clearance, out var safetyEnabled))
        {
            return false;
        }

        if (!safetyEnabled || requestedProbeDistance >= 0)
        {
            return true;
        }

        var lowestAllowedZ = minimumMachineZ + clearance;
        var currentMachineZ = MachineZ;
        var requestedTargetZ = currentMachineZ + requestedProbeDistance;
        var availableTravel = currentMachineZ - lowestAllowedZ;
        if (availableTravel <= 0.001d)
        {
            AddLog(
                $"{probeLabel} blocked: current machine Z {currentMachineZ:0.###} mm is at or below the guarded minimum " +
                $"{lowestAllowedZ:0.###} mm.");
            return false;
        }

        if (requestedTargetZ < lowestAllowedZ)
        {
            var originalProbeDistance = requestedProbeDistance;
            safeProbeDistance = lowestAllowedZ - currentMachineZ;
            AddLog(
                $"{probeLabel} command reduced from Z{originalProbeDistance:0.###} to Z{safeProbeDistance:0.###} " +
                $"so the target stays {clearance:0.###} mm above minimum machine Z {minimumMachineZ:0.###} mm.");
        }

        return Math.Abs(safeProbeDistance) > 0.001d;
    }

    private bool TryGetZProbeSafetySettings(out double minimumMachineZ, out double clearance, out bool safetyEnabled)
    {
        minimumMachineZ = 0;
        clearance = 0.1d;
        safetyEnabled = !string.IsNullOrWhiteSpace(ProbeMinimumMachineZInput);

        if (!safetyEnabled)
        {
            return true;
        }

        if (!TryParseDouble(ProbeMinimumMachineZInput, "minimum machine Z", out minimumMachineZ) ||
            !TryParseDouble(ProbeMinimumClearanceInput, "Z probe clearance", out clearance))
        {
            return false;
        }

        if (clearance < 0)
        {
            ShowValidationError("Enter a non-negative Z probe clearance.");
            return false;
        }

        if (clearance < MinimumZProbeLimitClearance)
        {
            AddLog(
                $"Z probe clearance increased from {clearance:0.###} mm to {MinimumZProbeLimitClearance:0.###} mm so -Z probe moves stop short of the machine limit.");
            clearance = MinimumZProbeLimitClearance;
        }

        return true;
    }

    private void StoreDirectionalPartTouch(string axisLetter, int direction, ProbeWorkTouch touch)
    {
        if (axisLetter == "X")
        {
            if (direction < 0)
            {
                _lastPartProbeXMinus = touch.MachineX;
            }
            else
            {
                _lastPartProbeXPlus = touch.MachineX;
            }
        }
        else if (axisLetter == "Y")
        {
            if (direction < 0)
            {
                _lastPartProbeYMinus = touch.MachineY;
            }
            else
            {
                _lastPartProbeYPlus = touch.MachineY;
            }
        }

        CalculateOutsideCylinderCommand.RaiseCanExecuteChanged();
    }

    private void StoreMeasuredPartEdge(string axisLetter, int direction, ProbeWorkTouch touch, double tipRadius)
    {
        var normalizedAxis = axisLetter.ToUpperInvariant();
        var machineSurface = GetMachineAxis(touch, normalizedAxis) + (direction < 0 ? -tipRadius : tipRadius);
        var workSurface = GetWorkAxis(touch, normalizedAxis) + (direction < 0 ? -tipRadius : tipRadius);
        _lastMeasuredPartEdge = new PartProbeMeasuredEdge(
            normalizedAxis,
            direction,
            machineSurface,
            workSurface,
            $"{normalizedAxis}{FormatDirection(direction)}");
        OnPartProbeMeasurementChanged();
    }

    private void SetPartProbeMeasurePoint(bool isMeasureA)
    {
        if (_lastMeasuredPartEdge is null)
        {
            ShowValidationError("Probe an X or Y edge before setting a measure point.");
            return;
        }

        if (isMeasureA)
        {
            _partProbeMeasureA = _lastMeasuredPartEdge;
        }
        else
        {
            _partProbeMeasureB = _lastMeasuredPartEdge;
        }

        OnPartProbeMeasurementChanged();
        AddLog(
            $"Part probe measure {(isMeasureA ? "A" : "B")} set from {_lastMeasuredPartEdge.Label} at machine {_lastMeasuredPartEdge.Axis} {_lastMeasuredPartEdge.MachineSurface:0.###} mm.");
    }

    private bool CanSetPartProbeMeasurePoint()
    {
        return IsMillMode && _lastMeasuredPartEdge is not null;
    }

    private void ClearPartProbeMeasurement()
    {
        _partProbeMeasureA = null;
        _partProbeMeasureB = null;
        OnPartProbeMeasurementChanged();
        AddLog("Part probe edge measurement cleared.");
    }

    private async Task SetPartProbeMeasurementCenterZeroAsync()
    {
        if (!TryGetValidPartProbeMeasurement(out var axis, out var centerMachine, out var distance))
        {
            return;
        }

        var currentWorkValueForCenterZero = axis == "X"
            ? MachineX - centerMachine
            : MachineY - centerMachine;

        if (axis == "X")
        {
            await SetWorkCoordinateAsync(xValue: currentWorkValueForCenterZero);
        }
        else
        {
            await SetWorkCoordinateAsync(yValue: currentWorkValueForCenterZero);
        }

        PartProbeResultText =
            $"{axis} feature center zero set from edge measure. Distance {distance:0.###} mm, center machine {axis} {centerMachine:0.###} mm.";
        AddLog(PartProbeResultText);
        OnPartProbeMeasurementChanged();
    }

    private bool CanSetPartProbeMeasurementCenterZero()
    {
        return IsMillMode &&
               CanOffsetOrJog() &&
               TryGetValidPartProbeMeasurement(showValidation: false, out _, out _, out _);
    }

    private bool TryGetValidPartProbeMeasurement(out string axis, out double centerMachine, out double distance)
    {
        return TryGetValidPartProbeMeasurement(showValidation: true, out axis, out centerMachine, out distance);
    }

    private bool TryGetValidPartProbeMeasurement(bool showValidation, out string axis, out double centerMachine, out double distance)
    {
        axis = string.Empty;
        centerMachine = 0;
        distance = 0;

        if (_partProbeMeasureA is null || _partProbeMeasureB is null)
        {
            if (showValidation)
            {
                ShowValidationError("Set both Measure A and Measure B from probed edges before setting center zero.");
            }

            return false;
        }

        if (!string.Equals(_partProbeMeasureA.Axis, _partProbeMeasureB.Axis, StringComparison.OrdinalIgnoreCase))
        {
            if (showValidation)
            {
                ShowValidationError("Measure A and Measure B must be on the same axis.");
            }

            return false;
        }

        axis = _partProbeMeasureA.Axis;
        centerMachine = GetPartProbeMeasurementCenterMachine();
        distance = Math.Abs(_partProbeMeasureB.MachineSurface - _partProbeMeasureA.MachineSurface);
        return true;
    }

    private double GetPartProbeMeasurementCenterMachine()
    {
        if (_partProbeMeasureA is null || _partProbeMeasureB is null)
        {
            return 0;
        }

        return (_partProbeMeasureA.MachineSurface + _partProbeMeasureB.MachineSurface) / 2d;
    }

    private double GetWorkCoordinateAtMachinePosition(string axisLetter, double machinePosition)
    {
        return axisLetter.ToUpperInvariant() switch
        {
            "X" => machinePosition + (WorkX - MachineX),
            "Y" => machinePosition + (WorkY - MachineY),
            _ => machinePosition
        };
    }

    private void OnPartProbeMeasurementChanged()
    {
        OnPropertyChanged(nameof(PartProbeMeasurementText));
        SetPartProbeMeasureACommand.RaiseCanExecuteChanged();
        SetPartProbeMeasureBCommand.RaiseCanExecuteChanged();
        SetPartProbeMeasurementCenterZeroCommand.RaiseCanExecuteChanged();
    }

    private double GetCompensatedSurfaceCoordinate(string axisLetter, int direction, ProbeWorkTouch touch, double tipRadius)
    {
        var touchedCoordinate = GetWorkAxis(touch, axisLetter);
        return direction < 0
            ? touchedCoordinate - tipRadius
            : touchedCoordinate + tipRadius;
    }

    private ProbeWorkTouch SnapshotCurrentProbeTouch()
    {
        return new ProbeWorkTouch(MachineX, MachineY, MachineZ, MachineA, MachineB, WorkX, WorkY, WorkZ, WorkA, WorkB);
    }

    private ProbeWorkTouch ConvertProbeTouch(ProbeTouch touch)
    {
        return new ProbeWorkTouch(
            touch.MachineX,
            touch.MachineY,
            touch.MachineZ,
            touch.MachineA,
            touch.MachineB,
            touch.MachineX + (WorkX - MachineX),
            touch.MachineY + (WorkY - MachineY),
            touch.MachineZ + (WorkZ - MachineZ),
            touch.MachineA + (WorkA - MachineA),
            touch.MachineB + (WorkB - MachineB));
    }

    private static double GetWorkAxis(ProbeWorkTouch touch, string axisLetter)
    {
        return axisLetter.ToUpperInvariant() switch
        {
            "X" => touch.WorkX,
            "Y" => touch.WorkY,
            "Z" => touch.WorkZ,
            "A" => touch.WorkA,
            "B" => touch.WorkB,
            _ => 0
        };
    }

    private static double GetMachineAxis(ProbeWorkTouch touch, string axisLetter)
    {
        return axisLetter.ToUpperInvariant() switch
        {
            "X" => touch.MachineX,
            "Y" => touch.MachineY,
            "Z" => touch.MachineZ,
            "A" => touch.MachineA,
            "B" => touch.MachineB,
            _ => 0
        };
    }

    private double GetCurrentWorkAxis(string axisLetter)
    {
        return axisLetter.ToUpperInvariant() switch
        {
            "X" => WorkX,
            "Y" => WorkY,
            "Z" => WorkZ,
            "A" => WorkA,
            "B" => WorkB,
            _ => 0
        };
    }

    private double GetCurrentMachineAxis(string axisLetter)
    {
        return axisLetter.ToUpperInvariant() switch
        {
            "X" => MachineX,
            "Y" => MachineY,
            "Z" => MachineZ,
            "A" => MachineA,
            "B" => MachineB,
            _ => 0
        };
    }

    private static string FormatAxisValue(string axisLetter, double value)
    {
        var unit = axisLetter is "A" or "B" ? "deg" : "mm";
        return $"{axisLetter} {value:0.###} {unit}";
    }

    private static string FormatDirection(int direction)
    {
        return direction < 0 ? "-" : "+";
    }

    private static bool TryGetProbeDirection(string rawDirection, out string axisLetter, out int direction)
    {
        var normalized = NormalizeProbeDirection(rawDirection, string.Empty);
        if (normalized.Length == 2 && (normalized[0] is 'X' or 'Y') && (normalized[1] is '-' or '+'))
        {
            axisLetter = normalized[0].ToString();
            direction = normalized[1] == '-' ? -1 : 1;
            return true;
        }

        axisLetter = string.Empty;
        direction = 0;
        return false;
    }

    private static string NormalizeProbeDirection(string rawDirection, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(rawDirection)
            ? fallback
            : rawDirection.Trim().ToUpperInvariant();

        return normalized is "X-" or "X+" or "Y-" or "Y+"
            ? normalized
            : fallback;
    }

    private void ClearMillToolProbeCalibration(string reason)
    {
        if (!IsMillMode || (!_millProbeReferenceWorkZ.HasValue && !_millLastProbeWorkZ.HasValue))
        {
            return;
        }

        _millProbeReferenceWorkZ = null;
        _millLastProbeWorkZ = null;
        OnPropertyChanged(nameof(MillToolProbeStatusText));
        AddLog(reason);
    }

    private async Task MoveToMillToolChangeAsync(double toolChangeX, double toolChangeY, double safeZ)
    {
        await _grblClient.MoveToMachineAsync(z: safeZ);
        await _grblClient.MoveToMachineAsync(x: toolChangeX, y: toolChangeY);
    }

    private async Task WaitForMillProbeStatusAsync(double startingMachineZ, double startingWorkZ)
    {
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(50);

            if ((ProbePinHigh || Math.Abs(MachineZ - startingMachineZ) > 0.0005d) &&
                Math.Abs(WorkZ - startingWorkZ) > 0.0005d)
            {
                return;
            }
        }

        AddLog("Probe finished before a fresh status report arrived; using the latest reported position.");
    }

    private async Task WaitForProbePinStateAsync(bool expectedHigh, string timeoutMessage)
    {
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (ProbePinHigh == expectedHigh)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new InvalidOperationException(timeoutMessage);
    }

    private async Task<bool> WaitForMachineZAsync(double expectedMachineZ)
    {
        const int maxAttempts = 40;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (Math.Abs(MachineZ - expectedMachineZ) <= 0.05d)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    private async Task ReturnMillProbeToSafeZAsync(double safeZ)
    {
        await _grblClient.MoveToMachineAsync(z: safeZ);
        if (!await WaitForMachineZAsync(safeZ))
        {
            AddLog("Probe cycle completed, but status did not confirm the return-to-safe-Z move in time.");
        }
    }

    private async Task<(double MachineZ, double WorkZ)> ExecuteMillProbeCycleAsync(
        double probeMachineX,
        double probeMachineY,
        double safeZ,
        double probeStartZ,
        double probeTravel,
        double probeFeed,
        double probeFineFeed,
        double probeRetract)
    {
        await MoveToMillToolChangeAsync(probeMachineX, probeMachineY, safeZ);
        await _grblClient.MoveToMachineAsync(z: probeStartZ);
        if (!await WaitForMachineZAsync(probeStartZ))
        {
            AddLog("Probe start Z move completed, but status did not confirm the machine Z before probing.");
        }

        await WaitForProbePinStateAsync(
            expectedHigh: false,
            "Probe input is active before the tool probe cycle. Check the probe plate, wiring, or $6 NO/NC mode.");

        var startingMachineZ = MachineZ;
        var startingWorkZ = WorkZ;
        if (!TryGetSafeDownwardZProbeTravel(probeTravel, "tool coarse probe", out var coarseProbeTravel))
        {
            throw new InvalidOperationException("Tool probe stopped because there is not enough safe machine Z travel remaining.");
        }

        await _grblClient.ProbeAxisRelativeAsync("Z", -coarseProbeTravel, probeFeed);
        await WaitForMillProbeStatusAsync(startingMachineZ, startingWorkZ);

        var coarseProbeMachineZ = MachineZ;
        await _grblClient.MoveToMachineAsync(z: coarseProbeMachineZ + Math.Abs(probeRetract));
        await WaitForProbePinStateAsync(expectedHigh: false, "Probe input stayed active after the first pull-off. Increase pull-off or check the probe plate wiring.");

        var fineProbeStartMachineZ = MachineZ;
        var fineProbeStartWorkZ = WorkZ;
        var fineProbeTravel = Math.Max(Math.Abs(probeRetract) * 2d, 1d);
        if (!TryGetSafeDownwardZProbeTravel(fineProbeTravel, "tool fine probe", out fineProbeTravel))
        {
            throw new InvalidOperationException("Tool fine probe stopped because there is not enough safe machine Z travel remaining.");
        }

        await _grblClient.ProbeAxisRelativeAsync("Z", -fineProbeTravel, probeFineFeed);
        await WaitForMillProbeStatusAsync(fineProbeStartMachineZ, fineProbeStartWorkZ);

        var confirmedMachineZ = MachineZ;
        var confirmedWorkZ = WorkZ;
        return (confirmedMachineZ, confirmedWorkZ);
    }

    private async Task EnsureSelectedWorkCoordinateSystemActiveAsync()
    {
        var normalizedWcs = NormalizeWorkCoordinateSystem(SelectedWorkCoordinateSystem);
        if (string.Equals(_lastAppliedWorkCoordinateSystem, normalizedWcs, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _grblClient.SelectWorkCoordinateSystemAsync(normalizedWcs);
        _lastAppliedWorkCoordinateSystem = normalizedWcs;
        AddLog($"Selected {normalizedWcs} as the active work coordinate system.");
    }

    private bool TryGetMillToolChangeSettings(out double toolChangeX, out double toolChangeY, out double safeZ)
    {
        toolChangeX = 0;
        toolChangeY = 0;
        safeZ = 0;

        if (!TryParseDouble(ToolChangeXInput, "tool change X", out toolChangeX) ||
            !TryParseDouble(ToolChangeYInput, "tool change Y", out toolChangeY) ||
            !TryParseDouble(ToolChangeSafeZInput, "safe Z", out safeZ))
        {
            return false;
        }

        return true;
    }

    private bool TryGetMillProbeSettings(
        out double referencePlateX,
        out double referencePlateY,
        out double safeZ,
        out double probeStartZ,
        out double probeTravel,
        out double probeFeed,
        out double probeFineFeed,
        out double probeRetract)
    {
        referencePlateX = 0;
        referencePlateY = 0;
        safeZ = 0;
        probeStartZ = 0;
        probeTravel = 0;
        probeFeed = 0;
        probeFineFeed = 0;
        probeRetract = 0;

        if (!TryGetMillToolChangeSettings(out _, out _, out safeZ) ||
            !TryParseDouble(ReferencePlateXInput, "reference plate X", out referencePlateX) ||
            !TryParseDouble(ReferencePlateYInput, "reference plate Y", out referencePlateY) ||
            !TryParseDouble(ProbeStartZInput, "probe start Z", out probeStartZ) ||
            !TryParseDouble(ProbeTravelInput, "probe travel", out probeTravel) ||
            !TryParseDouble(ProbeFeedInput, "probe feed", out probeFeed) ||
            !TryParseDouble(ProbeFineFeedInput, "fine probe feed", out probeFineFeed) ||
            !TryParseDouble(ProbeRetractInput, "probe retract", out probeRetract))
        {
            return false;
        }

        if (probeTravel <= 0 || probeFeed <= 0 || probeFineFeed <= 0 || probeRetract <= 0)
        {
            ShowValidationError("Enter positive probe travel, coarse probe feed, fine probe feed, and pull-off values.");
            return false;
        }

        if (probeStartZ > safeZ)
        {
            ShowValidationError("Probe start Z should be at or below the safe Z height.");
            return false;
        }

        return true;
    }

    private bool TryGetToolSetterSettings(out double toolSetterX, out double toolSetterY, out double toolSetterProbeZ, out double toolSetterOffset)
    {
        toolSetterOffset = 0;

        return TryGetToolSetterLocationSettings(out toolSetterX, out toolSetterY, out toolSetterProbeZ) &&
               TryParseDouble(ToolSetterOffsetInput, "tool setter offset", out toolSetterOffset);
    }

    private bool TryGetToolSetterLocationSettings(out double toolSetterX, out double toolSetterY, out double toolSetterProbeZ)
    {
        toolSetterX = 0;
        toolSetterY = 0;
        toolSetterProbeZ = 0;

        return TryParseDouble(ToolSetterXInput, "tool setter X", out toolSetterX) &&
               TryParseDouble(ToolSetterYInput, "tool setter Y", out toolSetterY) &&
               TryParseDouble(ToolSetterProbeZInput, "tool setter probe Z", out toolSetterProbeZ);
    }

    private bool TryGetMillPlanarFeedRate(out double feedRate)
    {
        if (!TryParseDouble(XJogFeedInput, "X jog feed", out var xFeedRate) ||
            !TryParseDouble(YJogFeedInput, "Y jog feed", out var yFeedRate))
        {
            feedRate = 0;
            return false;
        }

        if (xFeedRate <= 0 || yFeedRate <= 0)
        {
            ShowValidationError("Enter positive X and Y jog feeds before moving to work X/Y zero.");
            feedRate = 0;
            return false;
        }

        feedRate = Math.Min(xFeedRate, yFeedRate);
        return true;
    }

    private static string BuildOffsetLogMessage(double? xValue, double? yValue, double? zValue, double? aValue, double? bValue, string workCoordinateSystem)
    {
        var parts = new List<string>();
        if (xValue.HasValue)
        {
            parts.Add($"X={xValue.Value:0.###}");
        }

        if (yValue.HasValue)
        {
            parts.Add($"Y={yValue.Value:0.###}");
        }

        if (zValue.HasValue)
        {
            parts.Add($"Z={zValue.Value:0.###}");
        }

        if (aValue.HasValue)
        {
            parts.Add($"A={aValue.Value:0.###}");
        }

        if (bValue.HasValue)
        {
            parts.Add($"B={bValue.Value:0.###}");
        }

        var normalizedWcs = NormalizeWorkCoordinateSystem(workCoordinateSystem);
        return parts.Count == 0
            ? $"Updated {normalizedWcs} work offset."
            : $"Updated {normalizedWcs} work offset: {string.Join(", ", parts)}";
    }

    private bool TryParseDouble(string rawValue, string fieldName, out double parsedValue)
    {
        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
        {
            return true;
        }

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out parsedValue))
        {
            return true;
        }

        ShowValidationError($"Enter a valid number for {fieldName}.");
        return false;
    }

    private static bool TryParseToolNumber(string rawValue, out int toolNumber)
    {
        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out toolNumber) && toolNumber >= 0)
        {
            return true;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out toolNumber) && toolNumber >= 0)
        {
            return true;
        }

        toolNumber = 0;
        return false;
    }

    private static bool TryParsePositiveInt(string rawValue, out int parsedValue)
    {
        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue) && parsedValue > 0)
        {
            return true;
        }

        return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsedValue) && parsedValue > 0;
    }

    private static string FormatControllerSetting(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool TryParseFlexibleDouble(string rawValue, out double value)
    {
        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static string NormalizeWorkCoordinateSystem(string? workCoordinateSystem)
    {
        var normalized = string.IsNullOrWhiteSpace(workCoordinateSystem)
            ? "G54"
            : workCoordinateSystem.Trim().ToUpperInvariant();

        return normalized is "G54" or "G55" or "G56" or "G57" or "G58" or "G59"
            ? normalized
            : "G54";
    }

    private sealed record ProbeTouch(
        double MachineX,
        double MachineY,
        double MachineZ,
        double MachineA,
        double MachineB);

    private sealed record ProbeWorkTouch(
        double MachineX,
        double MachineY,
        double MachineZ,
        double MachineA,
        double MachineB,
        double WorkX,
        double WorkY,
        double WorkZ,
        double WorkA,
        double WorkB);

    private sealed record PartProbeMeasuredEdge(
        string Axis,
        int Direction,
        double MachineSurface,
        double WorkSurfaceAtTouch,
        string Label);
}
