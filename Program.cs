using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Daten-Speicher
var benutzerListe = new List<BenutzerKonto>();
var anfragenListe = new List<TerminAnfrage>();
int idCounter = 1;

// INITIALER PROBE-ACCOUNT FÜR DIE PRAXIS
benutzerListe.Add(new BenutzerKonto
{
    Benutzername = "praxis.kiefer",
    Email = "info@praxis-kiefer.de",
    Passwort = "123",
    Rolle = "Praxis",
    Name = "Praxis Dr. med. Heike Kiefer",
    Vorname = "Heike",
    Nachname = "Kiefer",
    TitelRolle = "Fachärztin für Allgemeinmedizin",
    Strasse = "Musterstraße 1",
    Ort = "91249 Neuhaus a.d. Pegnitz",
    Telefon = "09156 / 1234",
    Leistungen = new List<string> { "Akut-Sprechstunde", "Routine / Check-up", "Blutentnahme", "Impfung", "EKG / Belastungs-EKG" }
});

var oeffnungszeiten = new List<Sprechzeit>
{
    new Sprechzeit("Montag", "08:00 - 11:30 Uhr", "16:00 - 18:00 Uhr"),
    new Sprechzeit("Dienstag", "08:00 - 11:30 Uhr", "17:00 - 19:00 Uhr"),
    new Sprechzeit("Mittwoch", "08:00 - 11:30 Uhr", "Geschlossen"),
    new Sprechzeit("Donnerstag", "08:00 - 11:30 Uhr", "16:00 - 18:00 Uhr"),
    new Sprechzeit("Freitag", "08:00 - 11:30 Uhr", "Nachmittags n. V.")
};

// --- API ENDPUNKTE ---

app.MapGet("/api/oeffnungszeiten", () => Results.Ok(oeffnungszeiten));

// Alle registrierten Praxen für Patienten abrufen (inkl. deren Leistungen)
app.MapGet("/api/praxen", () =>
{
    var praxen = benutzerListe.Where(b => b.Rolle == "Praxis").Select(p => new {
        p.Benutzername,
        p.Name,
        p.TitelRolle,
        p.Ort,
        p.Leistungen
    });
    return Results.Ok(praxen);
});

// 1. REGISTRIERUNG
app.MapPost("/api/auth/registrieren", (RegistrierungsRequest req) =>
{
    var benutzernameBesetzt = benutzerListe.Any(b => b.Benutzername.Equals(req.Benutzername, StringComparison.OrdinalIgnoreCase));
    var emailBesetzt = benutzerListe.Any(b => b.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase));

    if (benutzernameBesetzt) return Results.BadRequest("Der Benutzername ist bereits vergeben.");
    if (emailBesetzt) return Results.BadRequest("Diese E-Mail-Adresse wird bereits verwendet.");

    var leistungList = new List<string>();
    if (!string.IsNullOrWhiteSpace(req.Leistungen))
    {
        leistungList = req.Leistungen.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
    else if (req.Rolle == "Praxis")
    {
        leistungList = new List<string> { "Akut-Sprechstunde", "Routine / Check-up", "Beratung" };
    }

    var neuerBenutzer = new BenutzerKonto
    {
        Benutzername = req.Benutzername,
        Email = req.Email,
        Passwort = req.Passwort,
        Rolle = req.Rolle,
        Name = req.Rolle == "Praxis" ? req.PraxisName : $"{req.Vorname} {req.Nachname}",
        Vorname = req.Vorname,
        Nachname = req.Nachname,
        Geburtsdatum = req.Geburtsdatum,
        Versichertennummer = req.Versichertennummer,
        Strasse = req.Strasse,
        Ort = req.Ort,
        Telefon = req.Telefon,
        TitelRolle = req.TitelRolle,
        Leistungen = leistungList
    };

    benutzerListe.Add(neuerBenutzer);
    return Results.Ok(neuerBenutzer);
});

// 2. LEISTUNGEN NACHTRÄGLICH BEARBEITEN (Praxis)
app.MapPost("/api/praxis/leistungen", (LeistungenUpdateReq req) =>
{
    var praxis = benutzerListe.FirstOrDefault(b => b.Benutzername.Equals(req.Benutzername, StringComparison.OrdinalIgnoreCase) && b.Rolle == "Praxis");
    if (praxis == null) return Results.NotFound("Praxis nicht gefunden.");

    praxis.Leistungen = req.Leistungen ?? new List<string>();
    return Results.Ok(praxis);
});

// 3. LOGIN
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    var benutzer = benutzerListe.FirstOrDefault(b =>
        (b.Benutzername.Equals(req.LoginKennung, StringComparison.OrdinalIgnoreCase) ||
         b.Email.Equals(req.LoginKennung, StringComparison.OrdinalIgnoreCase)) &&
        b.Passwort == req.Passwort
    );

    if (benutzer == null) return Results.Unauthorized();

    return Results.Ok(benutzer);
});

// 4. WUNSCHTERMIN ANFRAGEN
app.MapPost("/api/anfragen/neu", (TerminWunschRequest req) =>
{
    var neueAnfrage = new TerminAnfrage
    {
        Id = idCounter++,
        Benutzername = req.Benutzername,
        PraxisBenutzername = req.PraxisBenutzername,
        Vorname = req.Vorname,
        Nachname = req.Nachname,
        Geburtsdatum = req.Geburtsdatum,
        Versichertennummer = req.Versichertennummer,
        Strasse = req.Strasse,
        Ort = req.Ort,
        Telefon = req.Telefon,
        Email = req.Email,
        Besuchsgrund = req.Besuchsgrund,
        WunschDatumUhrzeit = req.WunschDatumUhrzeit,
        Status = "Ausstehend",
        Gegenvorschlag = null
    };

    anfragenListe.Add(neueAnfrage);
    return Results.Ok(neueAnfrage);
});

// 5. ANFRAGEN FÜR PRAXIS ABFRUFEN
app.MapGet("/api/anfragen/praxis/{praxisUser}", (string praxisUser) =>
{
    var treffer = anfragenListe.Where(a => string.Equals(a.PraxisBenutzername, praxisUser, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(a.PraxisBenutzername)).ToList();
    return Results.Ok(treffer);
});

// 6. PRAXIS BEARBEITUNG
app.MapPost("/api/anfragen/bearbeiten", (BearbeitenRequest req) =>
{
    var anfrage = anfragenListe.FirstOrDefault(a => a.Id == req.AnfrageId);
    if (anfrage == null) return Results.NotFound();

    if (req.Aktion == "annehmen") { anfrage.Status = "Bestätigt"; anfrage.Gegenvorschlag = null; }
    else if (req.Aktion == "vorschlagen") { anfrage.Status = "Alternativvorschlag"; anfrage.Gegenvorschlag = req.AlternativDatumUhrzeit; }
    else if (req.Aktion == "ablehnen") { anfrage.Status = "Abgelehnt"; }

    return Results.Ok(anfrage);
});

// 7. PATIENTEN ANTWORT
app.MapPost("/api/anfragen/patient-antwort", (PatientAntwortRequest req) =>
{
    var anfrage = anfragenListe.FirstOrDefault(a => a.Id == req.AnfrageId);
    if (anfrage == null) return Results.NotFound();

    if (req.Aktion == "annehmen")
    {
        if (anfrage.Gegenvorschlag.HasValue) anfrage.WunschDatumUhrzeit = anfrage.Gegenvorschlag.Value;
        anfrage.Status = "Bestätigt";
        anfrage.Gegenvorschlag = null;
    }
    else if (req.Aktion == "ablehnen") { anfrage.Status = "Abgelehnt"; }
    else if (req.Aktion == "neuer_wunsch" && req.NeuerWunsch.HasValue)
    {
        anfrage.WunschDatumUhrzeit = req.NeuerWunsch.Value;
        anfrage.Status = "Ausstehend";
        anfrage.Gegenvorschlag = null;
    }

    return Results.Ok(anfrage);
});

// 8. PATIENTEN ANFRAGEN ABFRUFEN
app.MapGet("/api/anfragen/patient/{benutzername}", (string benutzername) =>
{
    var treffer = anfragenListe.Where(a => a.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase)).ToList();
    return Results.Ok(treffer);
});

app.MapGet("/patient", async ctx => { ctx.Response.Redirect("/patient.html"); });
app.MapGet("/praxis", async ctx => { ctx.Response.Redirect("/praxis.html"); });

app.Run();

// MODELLE
record Sprechzeit(string Tag, string Vormittag, string Nachmittag);

class BenutzerKonto
{
    public string Benutzername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Passwort { get; set; } = string.Empty;
    public string Rolle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Vorname { get; set; } = string.Empty;
    public string Nachname { get; set; } = string.Empty;
    public string Geburtsdatum { get; set; } = string.Empty;
    public string Versichertennummer { get; set; } = string.Empty;
    public string Strasse { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
    public string TitelRolle { get; set; } = string.Empty;
    public List<string> Leistungen { get; set; } = new List<string>();
}

class TerminAnfrage
{
    public int Id { get; set; }
    public string Benutzername { get; set; } = string.Empty;
    public string PraxisBenutzername { get; set; } = string.Empty;
    public string Vorname { get; set; } = string.Empty;
    public string Nachname { get; set; } = string.Empty;
    public string Geburtsdatum { get; set; } = string.Empty;
    public string Versichertennummer { get; set; } = string.Empty;
    public string Strasse { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Besuchsgrund { get; set; } = string.Empty;
    public DateTime WunschDatumUhrzeit { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? Gegenvorschlag { get; set; }
}

record RegistrierungsRequest(string Benutzername, string Email, string Passwort, string Rolle, string PraxisName, string Vorname, string Nachname, string Geburtsdatum, string Versichertennummer, string Strasse, string Ort, string Telefon, string TitelRolle, string Leistungen);
record LoginRequest(string LoginKennung, string Passwort);
record TerminWunschRequest(string Benutzername, string PraxisBenutzername, string Vorname, string Nachname, string Geburtsdatum, string Versichertennummer, string Strasse, string Ort, string Telefon, string Email, string Besuchsgrund, DateTime WunschDatumUhrzeit);
record BearbeitenRequest(int AnfrageId, string Aktion, DateTime? AlternativDatumUhrzeit);
record PatientAntwortRequest(int AnfrageId, string Aktion, DateTime? NeuerWunsch);
record LeistungenUpdateReq(string Benutzername, List<string> Leistungen);