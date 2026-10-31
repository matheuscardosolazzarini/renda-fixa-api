namespace FixedIncome.Application.DTOs;

// CdiDiscarded conta as cotações de CDI descartadas por cair em dia não útil.
public record ImportIndexRatesResponse(
    int CdiImported,
    int CdiDiscarded,
    int IpcaImported);
