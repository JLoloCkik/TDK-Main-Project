using System;
using System.Collections.Generic;

namespace Kreta.Core;

/// <summary>
/// Altalanos celu, rugalmas entitas TETSZOLEGES uj funkcio (pl. hirdetotabla / NoticeMessage,
/// esemenynaptar, szavazas, uzenofal, stb.) perzisztalasara.
///
/// CEL: Az onfejlodo rendszerben a felhasznalo szazaval kerhet uj, elore nem ismert funkciokat.
/// Nem realis, hogy minden egyes uj fogalomhoz egy fejleszto (Claude) kezzel felvegyen egy uj
/// C# domain osztalyt es uj Context-metodust - ez mar NEM onfejlodes, hanem hagyomanyos fejlesztes.
///
/// Ehelyett a Gemini altal generalt kod ezt az EGY, mar letezo entitast es a Context-eken elerheto
/// QueryEntities / GetEntity / SaveEntity / DeleteEntity metodusokat hasznalja, sajat maga valasztott
/// `EntityType` cimkevel (pl. "NoticeMessage") es tetszoleges kulcs-ertek adattal a `Data` mezoben.
/// Igy a Gemini valoban ONALLOAN, ujrafordulas nelkul tud brand-new funkciokat letrehozni.
/// </summary>
public class GenericRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Az AI altal szabadon valasztott, funkciot azonosito cimke (pl. "NoticeMessage", "Esemeny", "Szavazas").
    /// Ugyanazon funkcio minden peldanya ugyanazt az EntityType erteket hasznalja.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Melyik szerepkorben jott letre a rekord (audit / RBAC celra).
    /// </summary>
    public Role CreatedByRole { get; set; }

    /// <summary>
    /// A letrehozo/utolso modosito felhasznalo azonositoja (opcionalis).
    /// </summary>
    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Tetszoleges kulcs-ertek adat (pl. "Cim" -> "Sportnap elmarad", "Szoveg" -> "...",
    /// "CelOsztaly" -> "9.A"). Az adatbazisban JSON-kent taroljuk.
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = new();
}