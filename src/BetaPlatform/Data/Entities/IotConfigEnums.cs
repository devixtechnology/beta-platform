namespace BetaPlatform.Data.Entities;

// IoT configuration enums, ported verbatim from the reference SPackEdgeView schema so the
// machine_tags / machines_kpis / machines_master_data / sources_available tables carry identical
// integer semantics. Stored as INT in the database.

public enum TagType
{
    RQA = 0,
    AQR = 1,
    CAL = 2
}

public enum DataSourceType
{
    ERP = 0,
    MES = 1,
    MID = 2,
    EDGE_SERVER = 3,
    PM = 4,
    MACHINE = 5,
    SCALE = 6,
    PRINTER = 7,
    IOT_DEVICE = 8
}

public enum DataModule
{
    OEE = 0,
    PM = 1
}

public enum TagDataType
{
    Number = 0,
    Bit = 1,
    Float = 2,
    String = 3
}
