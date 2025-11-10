# ? Correzione Entità HTML in CMTrace

## Problema

I log visualizzati in CMTrace mostravano **entità HTML** invece dei caratteri speciali:

```
? Active Sinks: &quot;AzureQueue, WebApi&quot;
? Value: x &lt; 10 &amp;&amp; y &gt; 5
? SQL: SELECT * WHERE name = &apos;test&apos;
```

## Root Cause

Il `CMTraceFormatter` stava facendo **escape dei caratteri XML** prima di scriverli nel log:

```csharp
// ? ERRATO
private static string EscapeXml(string text)
{
    return text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");
}

output.Write(EscapeXml(message));  // ? Causa &quot;, &lt;, etc.
```

### Perché Era Sbagliato

Il tag `<![LOG[...]LOG]!>` in CMTrace è una **sezione CDATA-like** che:
- ? Gestisce automaticamente i caratteri speciali
- ? Non richiede escape XML
- ? L'escape causa la visualizzazione delle entità HTML

## Soluzione

**Rimosso l'escape XML** dal formatter:

```csharp
// ? CORRETTO
public void Format(LogEvent logEvent, TextWriter output)
{
    var message = logEvent.RenderMessage();
    
    if (logEvent.Exception != null)
    {
        message = $"{message}{Environment.NewLine}{logEvent.Exception}";
    }

    // ? NON fare escape! Il tag <![LOG[...]LOG]!> è CDATA-like
    output.Write("<![LOG[");
    output.Write(message);  // ? Scritto direttamente
    output.Write("]LOG]!>");
    // ... resto del formato
}
```

## Risultato

### Prima (? Con Escape)

```
Sending HTTP request &quot;GET&quot; &quot;https://example.com&quot;
Error: x &lt; 10 &amp;&amp; y &gt; 5
Path: C:\Users\test
SQL: name = &apos;test&apos;
```

### Dopo (? Senza Escape)

```
Sending HTTP request "GET" "https://example.com"
Error: x < 10 && y > 5
Path: C:\Users\test
SQL: name = 'test'
```

## Files Modificati

### `SecureBootWatcher.Client\Logging\CMTraceFormatter.cs`

**Rimosso**:
```csharp
private static string EscapeXml(string text) { ... }  // ? Rimosso
```

**Aggiunto commento**:
```csharp
// NOTE: Do NOT escape XML characters in the message.
// The <![LOG[...]LOG]!> is a CDATA-like section and CMTrace handles special characters correctly.
// Escaping them causes &quot;, &lt;, etc. to appear in the log viewer.
```

## Testing

### Verifica Rapida

```powershell
# Genera nuovi log
cd SecureBootWatcher.Client\bin\Debug\net48
.\SecureBootWatcher.Client.exe

# Verifica che NON ci siano entità HTML
Get-Content "logs\client-*.log" | Select-String -Pattern '&quot;|&lt;|&gt;|&apos;|&amp;'
```

**Output atteso**: Nessun risultato ?

### Verifica in CMTrace

```powershell
CMTrace.exe "logs\client-*.log"
```

**Verifica che**:
- ? Virgolette appaiono come `"` non `&quot;`
- ? Apostrofi appaiono come `'` non `&apos;`
- ? Simboli `<` e `>` appaiono correttamente
- ? Ampersand `&` appare come `&`

## Deployment

### Rebuild Richiesto

```powershell
cd SecureBootWatcher.Client
dotnet clean
dotnet build -c Release
```

### Nessuna Configurazione Aggiuntiva

- ? Il formato CMTrace rimane il default
- ? Nessun cambio in `appsettings.json`
- ? Compatibile con deployment esistente

## Impact

### Nessun Breaking Change

- ? Formato CMTrace rimane compatibile
- ? Colorazione funziona come prima
- ? Filtri e ricerca funzionano
- ? Migliore leggibilità dei log

### Performance

- ? Leggermente **più veloce** (no escape overhead)
- ? Stesso footprint di memoria
- ? Stessa dimensione file

## Caratteri Supportati

Tutti i caratteri speciali ora appaiono correttamente:

| Carattere | Prima | Dopo |
|-----------|-------|------|
| `"` | `&quot;` ? | `"` ? |
| `'` | `&apos;` ? | `'` ? |
| `<` | `&lt;` ? | `<` ? |
| `>` | `&gt;` ? | `>` ? |
| `&` | `&amp;` ? | `&` ? |
| `\` | `\` ? | `\` ? |

## Summary

### Problema

? Caratteri speciali mostrati come entità HTML  
? Log poco leggibili in CMTrace  
? Escape XML non necessario  

### Soluzione

? Rimosso escape XML dal formatter  
? Caratteri speciali visualizzati correttamente  
? Log più leggibili  
? Performance leggermente migliorate  

---

**Status**: ? **CORRETTO**  
**Build Required**: Sì (rebuild client)  
**Config Changes**: No  
**Breaking Changes**: No  
**Documentation**: Aggiornata
