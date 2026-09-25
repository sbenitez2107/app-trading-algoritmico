using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// A fact about how one SQX-tracked instrument trades on FTMO's MT platform (design.md D4). Every
/// column is a user-supplied fact captured on 2026-09-24, carried with provenance — never a
/// system-derived or default value. Keyed by <see cref="SqxSymbol"/> verbatim; the FTMO symbol
/// mapping is declared here, never inferred from string similarity.
/// </summary>
public class FtmoInstrumentSpec : BaseEntity
{
    /// <summary>Verbatim SQX symbol (e.g. <c>XAUUSD_M1_UTC02</c>). Unique.</summary>
    public required string SqxSymbol { get; set; }

    /// <summary>The FTMO MT platform's symbol name for the same instrument (e.g. <c>GER40.cash</c>).</summary>
    public required string FtmoSymbol { get; set; }

    public decimal ContractSize { get; set; }

    /// <summary>ISO currency code the FTMO account profit/margin is denominated in.</summary>
    public required string ProfitCurrency { get; set; }

    public int SizeDecimals { get; set; }

    public decimal Step { get; set; }

    public decimal MinLot { get; set; }

    public decimal MaxLots { get; set; }

    /// <summary>IANA time zone ID for the source data's clock (e.g. <c>Asia/Jerusalem</c>). Never a fixed offset.</summary>
    public required string SourceTimeZoneId { get; set; }

    /// <summary>Required. States who supplied these values and when — never a system-chosen default.</summary>
    public required string Provenance { get; set; }

    public DateOnly CapturedOn { get; set; }
}
