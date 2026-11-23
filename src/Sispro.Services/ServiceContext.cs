using System.Text.Json;
using Sispro.Core.Dtos;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;
using Sispro.Data.Repositories;

namespace Sispro.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultSettingsFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cambiaso", "SISPRO", "settings.json");

    public static AppSettings Load(string? settingsPath = null)
    {
        string path = settingsPath ?? DefaultSettingsFilePath;
        AppSettings settings = new();

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    settings = loaded;
                }
            }
            catch
            {
                // Fallback to defaults if config file is corrupted
            }
        }

        if (string.IsNullOrWhiteSpace(settings.DatabasePath) || !File.Exists(settings.DatabasePath))
        {
            settings.DatabasePath = ResolveDefaultDatabasePath();
        }

        return settings;
    }

    public static void Save(AppSettings settings, string? settingsPath = null)
    {
        string path = settingsPath ?? DefaultSettingsFilePath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string ResolveDefaultDatabasePath()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cambiaso", "SISPRO", "sispro.db"),
            Path.Combine(baseDir, "Data", "sispro.db"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "sispro.db"))
        ];

        string userDb = candidates[0];
        if (File.Exists(userDb))
        {
            return userDb;
        }

        // Copy bundled initial database to writable LocalAppData so installed app under Program Files works without admin rights
        foreach (string seed in candidates.Skip(1))
        {
            if (File.Exists(seed))
            {
                string? userDir = Path.GetDirectoryName(userDb);
                if (!string.IsNullOrEmpty(userDir))
                {
                    Directory.CreateDirectory(userDir);
                }

                File.Copy(seed, userDb, overwrite: false);
                return userDb;
            }
        }

        return userDb;
    }
}

public sealed class SisproApplicationContext : IDisposable
{
    public AppSettings Settings { get; private set; }
    public SqliteConnectionFactory ConnectionFactory { get; private set; }
    public SisproSyncHub SyncHub { get; private set; }
    public IUserRepository Users { get; private set; }
    public IGroupRepository Groups { get; private set; }
    public IProviderRepository Providers { get; private set; }
    public IRequesterRepository Requesters { get; private set; }
    public IAssetTreeRepository AssetTree { get; private set; }
    public IWorkOrderRepository WorkOrders { get; private set; }
    public IInvoiceRepository Invoices { get; private set; }
    public IAuditRepository Audit { get; private set; }
    public IMaintenanceRepository Maintenance { get; private set; }

    public AuthService AuthService { get; private set; }
    public WorkOrderService WorkOrderService { get; private set; }
    public InvoiceService InvoiceService { get; private set; }
    public MaintainerService MaintainerService { get; private set; }
    public AssetTreeService AssetTreeService { get; private set; }
    public UserManagementService UserManagementService { get; private set; }
    public GroupManagementService GroupManagementService { get; private set; }
    public MaintenanceService MaintenanceService { get; private set; }
    public ExcelExportService ExcelExportService { get; private set; }
    public PdfExportService PdfExportService { get; private set; }
    public WordExportService WordExportService { get; private set; }
    public CsvExportService CsvExportService { get; private set; }
    public DocumentExportService DocumentExportService { get; private set; }
    public PrintService PrintService { get; private set; }

    public UserSession? CurrentSession { get; set; }

    public SisproApplicationContext(AppSettings settings, InitialAdminSeedOptions? initialAdmin = null)
    {
        Settings = settings;
        ConnectionFactory = new SqliteConnectionFactory(settings.DatabasePath);
        SchemaInitializer.EnsureInitialized(ConnectionFactory, initialAdmin);
        SyncHub = new SisproSyncHub(ConnectionFactory);

        Users = new UserRepository(ConnectionFactory);
        Groups = new GroupRepository(ConnectionFactory);
        Providers = new ProviderRepository(ConnectionFactory);
        Requesters = new RequesterRepository(ConnectionFactory);
        AssetTree = new AssetTreeRepository(ConnectionFactory);
        WorkOrders = new WorkOrderRepository(ConnectionFactory);
        Invoices = new InvoiceRepository(ConnectionFactory);
        Audit = new AuditRepository(ConnectionFactory);
        Maintenance = new MaintenanceRepository(ConnectionFactory);

        AuthService = new AuthService(Users, Audit);
        WorkOrderService = new WorkOrderService(WorkOrders, Providers, Requesters, AssetTree, Audit, SyncHub);
        InvoiceService = new InvoiceService(Invoices, WorkOrders, Providers, Audit, SyncHub);
        MaintainerService = new MaintainerService(Providers, Requesters, AssetTree, Audit, SyncHub);
        AssetTreeService = new AssetTreeService(AssetTree, Users, Audit, SyncHub);
        UserManagementService = new UserManagementService(Users, Requesters, Audit, Groups, SyncHub);
        GroupManagementService = new GroupManagementService(Groups, Users, Audit, SyncHub);
        MaintenanceService = new MaintenanceService(Maintenance);
        ExcelExportService = new ExcelExportService(settings);
        PdfExportService = new PdfExportService(settings);
        WordExportService = new WordExportService(settings);
        CsvExportService = new CsvExportService();
        DocumentExportService = new DocumentExportService(ExcelExportService, PdfExportService, WordExportService, CsvExportService);
        PrintService = new PrintService(settings);
    }

    public void ReloadDatabase(string newDatabasePath)
    {
        Settings.DatabasePath = newDatabasePath;
        AppSettingsStore.Save(Settings);

        ConnectionFactory = new SqliteConnectionFactory(newDatabasePath);
        SchemaInitializer.EnsureInitialized(ConnectionFactory);
        SyncHub.AttachDatabase(ConnectionFactory);

        Users = new UserRepository(ConnectionFactory);
        Groups = new GroupRepository(ConnectionFactory);
        Providers = new ProviderRepository(ConnectionFactory);
        Requesters = new RequesterRepository(ConnectionFactory);
        AssetTree = new AssetTreeRepository(ConnectionFactory);
        WorkOrders = new WorkOrderRepository(ConnectionFactory);
        Invoices = new InvoiceRepository(ConnectionFactory);
        Audit = new AuditRepository(ConnectionFactory);
        Maintenance = new MaintenanceRepository(ConnectionFactory);

        AuthService = new AuthService(Users, Audit);
        WorkOrderService = new WorkOrderService(WorkOrders, Providers, Requesters, AssetTree, Audit, SyncHub);
        InvoiceService = new InvoiceService(Invoices, WorkOrders, Providers, Audit, SyncHub);
        MaintainerService = new MaintainerService(Providers, Requesters, AssetTree, Audit, SyncHub);
        AssetTreeService = new AssetTreeService(AssetTree, Users, Audit, SyncHub);
        UserManagementService = new UserManagementService(Users, Requesters, Audit, Groups, SyncHub);
        GroupManagementService = new GroupManagementService(Groups, Users, Audit, SyncHub);
        MaintenanceService = new MaintenanceService(Maintenance);
        ExcelExportService = new ExcelExportService(Settings);
        PdfExportService = new PdfExportService(Settings);
        WordExportService = new WordExportService(Settings);
        CsvExportService = new CsvExportService();
        DocumentExportService = new DocumentExportService(ExcelExportService, PdfExportService, WordExportService, CsvExportService);
        PrintService = new PrintService(Settings);

        SyncHub.Publish(Sispro.Core.Enums.DataChangeKind.All, "RECARGAR_BASE_DATOS");
    }

    public void Dispose()
    {
        SyncHub.Dispose();
    }
}
