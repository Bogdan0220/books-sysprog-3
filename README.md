# BOOKS-SYSPROG (Projekat iz Sistemskog programiranja)

## Tekst zadatka
**Zadatak 27 (treći projekat):**  
Koristeći principe **Reaktivnog programiranja** i **Google Books API**, implementirati aplikaciju za prikaz opisa traženih knjiga (*description* property).  
Za prikupljene opise implementirati **Sentiment analizu** koristeći *SentimentAnalysis.NET* ili *ML.NET* biblioteke.  
Sentiment računati na nivou opisa i prikazati rezultate.  

Napomene:  
- Web server implementirati kao **konzolnu aplikaciju** koja loguje sve primljene zahteve i informacije o njihovoj obradi.  
- Kao klijentsku aplikaciju može se koristiti Web browser ili posebna konzolna aplikacija.  
- Za realizaciju koristiti biblioteku **Reactive Extensions for .NET (Rx)**.  
- Po defaultu, Rx je single-threaded rešenje; ukoliko se uključe multithreading i Scheduleri, dobija se veći broj poena.  

---

## Ideja rešenja
Aplikacija je implementirana kao mali **web server** u C# (.NET 8).  
Koristi se `HttpListener` da server prihvata GET zahteve i vraća JSON odgovore.  
Na svaku pretragu (`/books?q=...&max=...`) server:

1. Prima zahtev preko browsera.  
2. Loguje zahtev (metod, ruta, vreme obrade).  
3. Putem `BooksClient` klase kontaktira **Google Books API** i dobija listu knjiga.  
4. Pokreće **Rx pipeline**:  
   - `FromAsync` → asinhrono dobavljanje knjiga  
   - `SelectMany` → prolazak kroz svaku knjigu  
   - `Where` → preskače knjige bez opisa  
   - `ObserveOn(TaskPoolScheduler.Default)` → pokreće sentiment analizu paralelno na više niti  
   - `Select` → računa sentiment za svaki opis pomoću **ML.NET** modela  
   - `Merge + ToArray` → spaja rezultate nazad u JSON listu  
5. Kao odgovor vraća JSON sa poljima:  
   - `Title`  
   - `Authors`  
   - `Description`  
   - `SentimentScore` (verovatnoća da je pozitivan tekst)  
   - `SentimentLabel` (Positive/Negative)  

Pored toga postoji i health ruta:  
- `/health` → vraća `{ "status": "ok" }`  

---

## Tehnologije
- **.NET 8** (konzolna aplikacija)  
- **Reactive Extensions (Rx.NET)** → reaktivno programiranje  
- **ML.NET** → sentiment analiza teksta  
- **Google Books API** → izvor podataka o knjigama  

---
## Autori
- Lazar Krstić, 19190 
- Bogdan Jovanović, 19153  
- Elektronski fakultet, Univerzitet u Nišu