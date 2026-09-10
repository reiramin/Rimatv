using System.Text.RegularExpressions;

namespace iptv.Services._Channel.Constants;

public class CuratedChannelWhitelist
{
    private const string RawList = """
                                   IRIB TV1
                                   IRIB TV2
                                   IRIB TV3
                                   IRIB TV4
                                   IRIB TV5 / Tehran TV
                                   IRINN
                                   IRIB Amoozesh
                                   IRIB Quran
                                   IRIB Mostanad
                                   IRIB Namayesh
                                   IRIB Ofogh
                                   IRIB Varzesh
                                   IRIB Pooya & Nahal
                                   IRIB Salamat
                                   IRIB Nasim
                                   IRIB Omid
                                   IRIB Tamasha
                                   IRIB UHD/HDR
                                   iFilm Persian
                                   iFilm 2
                                   iFilm Arabic
                                   iFilm English
                                   Jame Jam TV 1
                                   Jame Jam TV 2
                                   Jame Jam TV 3
                                   Al-Alam News Network
                                   Al-Kawthar TV
                                   Al Wilayah TV
                                   Al Zahra TV
                                   Sahar TV Azeri
                                   Sahar TV Urdu
                                   Sahar TV Kurdi
                                   Sahar TV Balkan
                                   Press TV
                                   Press TV French
                                   Press TV English
                                   HispanTV
                                   IranPress
                                   Karbala Live 1
                                   Karbala Live 2
                                   Karbala Live 3
                                   Karbala Live 4
                                   Karbala Live 5
                                   Imam Hussein TV
                                   Manoto
                                   Iran International
                                   BBC Persian
                                   VOA Persian
                                   Radio Farda TV
                                   MBC Persia
                                   Farsi1
                                   Afghanistan International
                                   GEM TV
                                   GEM Classic
                                   GEM 24B
                                   GEM AZ
                                   GEM Arabia
                                   GEM Academy
                                   GEM Bollywood
                                   GEM Drama
                                   GEM Film
                                   GEM Fit
                                   GEM Food
                                   GEM Junior
                                   GEM Kids
                                   GEM Latino
                                   GEM Life
                                   GEM MAX
                                   GEM Modern Economy
                                   GEM Nature
                                   GEM Onyx
                                   GEM Property
                                   GEM Series
                                   GEM Travel
                                   Persiana +
                                   Persiana Billboard
                                   Persiana Cinema
                                   Persiana Classic
                                   Persiana Comedy
                                   Persiana Documentary
                                   Persiana Entertainment
                                   Persiana Family
                                   Persiana Family+
                                   Persiana Game & Tech
                                   Persiana Iranian
                                   Persiana Junior
                                   Persiana Music
                                   Persiana Nostalgia
                                   Persiana Medical
                                   Persiana Travel
                                   PMC
                                   PMC Royale
                                   PBC Tapesh TV
                                   Tapesh 2
                                   Pars TV
                                   Payvand TV
                                   Didgah TV
                                   Ganj e Hozour TV
                                   Mihan TV
                                   Negah TV
                                   Bazme Asheghan
                                   Andisheh TV
                                   Rangarang TV
                                   Parsiland TV
                                   Parnian TV
                                   Omid Javedan
                                   Persian Bazar
                                   4U TV (Iran)
                                   Cine Film
                                   Cineseries
                                   EBC1 TV
                                   Caltex Music TV
                                   Canada Star TV
                                   Meta Film TV
                                   HodHod Farsi TV
                                   Iran Nama
                                   Newflix
                                   Omide Iran
                                   Arax TV
                                   Arko TV
                                   Arvan TV
                                   Atrina TV
                                   Afra Film
                                   Afra Series
                                   AVA Family
                                   AVA Series
                                   Bravo Farsi TV
                                   Cafe Film
                                   Cafe Trade TV
                                   Classic TV
                                   Datis TV
                                   Dej TV
                                   EPlanet TV
                                   FX TV 1
                                   FX TV 2
                                   Gold Star
                                   Grand Cinema
                                   Home Plus
                                   Zed TV
                                   Kanal Jadid
                                   Khalij TV
                                   T2 TV
                                   Avang TV
                                   Ayeneh TV
                                   Payam Javan TV
                                   Payam-e-Afghan TV
                                   Payame Aramesh TV
                                   Iman TV
                                   High Vision TV
                                   AFN TV
                                   247 Box TV
                                   AMG TV
                                   Varzesh TV Farsi
                                   4 Kurd
                                   4 Turk Billboard
                                   4 Turk Music
                                   VOX1 TVG
                                   VOX2 TVG
                                   Simaye Azadi
                                   IraneFarda TV
                                   National Iranian Congress TV
                                   Iran National Revolution TV
                                   Komala TV
                                   Kurd Channel
                                   Mohabat TV
                                   TBN Nejat TV
                                   WiseHumanTv
                                   RasoulallahTv
                                   Kalemeh TV
                                   Sat7 Pars
                                   Sat7 Turk
                                   Setareh TV
                                   Asil TV
                                   Assirat TV
                                   Iran Jewish TV
                                   SNN TV
                                   OXIR TV
                                   Radio Javan TV
                                   Navahang TV
                                   MTC TV
                                   Shabakeh 7
                                   SL 1
                                   SL 2
                                   YourTime TV
                                   ITN TV
                                   ICC Plus
                                   icnet 1
                                   icnet 2
                                   icnet 3
                                   4 Afghanistan
                                   4 Music
                                   Tin TV
                                   4U TV (Turkey)
                                   360 TV
                                   A2TV
                                   A Haber
                                   A Spor
                                   Afroturk TV
                                   Aksu TV
                                   Al-Zahra TV Turkic
                                   Alanya Posta TV
                                   Almahriah TV
                                   Altas TV
                                   Anadolu Net TV
                                   ARAS TV
                                   ATV
                                   ATV Alanya
                                   ATV Avrupa
                                   Benguturk TV
                                   Beyaz TV
                                   Bir TV
                                   Bloomberg HT
                                   BRTV
                                   Bursa AS TV
                                   Bursa TV
                                   Cay TV
                                   Cekmeköy TV
                                   Çiftçi TV
                                   CNBC-e
                                   Deniz Postası TV
                                   DHA
                                   Disney Jr. (TR)
                                   Diyanet TV
                                   Diyar TV
                                   Dost TV
                                   Dream Türk
                                   Edessa TV
                                   Er TV
                                   Erzurum Web TV
                                   ES TV
                                   ETV Kayseri
                                   ETV Manisa
                                   Euro D
                                   FB TV
                                   Finans Turk TV
                                   Flash Haber TV
                                   Fortuna TV
                                   Guneydogu TV
                                   GZT
                                   Haber61 TV
                                   Haber Global
                                   Habertürk TV
                                   Halk TV
                                   HTSpor TV
                                   Hunat TV
                                   Icel TV
                                   Ilke TV
                                   Kanal 3
                                   Kanal 7
                                   Kanal 7 Avrupa
                                   Kanal 12
                                   Kanal 15
                                   Kanal 23
                                   Kanal 26
                                   Kanal 32
                                   Kanal 33
                                   Kanal 34
                                   Kanal 58
                                   Kanal D
                                   Kanal D Drama
                                   Kanal Firat
                                   Kanal Hayat
                                   Kanal V
                                   Kay TV
                                   Kent Türk TV
                                   Kocaeli TV
                                   Konya Olay TV
                                   KRAL Pop TV
                                   Lalegul TV
                                   Life TV
                                   Line TV
                                   Luys TV
                                   MaviKaradeniz
                                   Med Muzik
                                   Mekameleen TV
                                   Meltem TV
                                   Mercan TV
                                   Minika Cocuk
                                   Minika Go
                                   MovieSmart Turk
                                   MTürk TV
                                   National Geographic (TR)
                                   National Geographic Wild (TR)
                                   Natural TV
                                   NOW TV
                                   NTV
                                   Number 1 Ask
                                   Number 1 Damar
                                   Number 1 Dance
                                   Number 1 TV
                                   Olay Türk TV Kayseri
                                   On4 TV
                                   Power Dance
                                   Power Love
                                   Power Turk
                                   Power Türk Akustik
                                   Power Türk Slow
                                   Power Türk Taptaze
                                   Power TV
                                   Qaf TV
                                   S Sport
                                   S Sport 2
                                   Sat7 Türk (TR)
                                   Satranç TV
                                   Semerkand TV
                                   Sercem TV
                                   Show Turk
                                   Star TV
                                   Sun RTV
                                   TBMM TV
                                   Tele 1
                                   Tempo TV
                                   TGRT Belgesel TV
                                   TGRT Haber
                                   Tivi 6
                                   TJK TV
                                   TJK TV 2
                                   Ton TV
                                   Torba TV
                                   TR24 TV
                                   TRT 1
                                   TRT 2
                                   TRT 3
                                   TRT Arabi
                                   TRT Avaz
                                   TRT Belgesel
                                   TRT Cocuk
                                   TRT Diyanet Çocuk
                                   TRT EBA Ilkokul
                                   TRT EBA Lise
                                   TRT EBA Ortaokul
                                   TRT Haber
                                   TRT Kurdî
                                   TRT Muzik
                                   TRT Spor
                                   TRT Spor Yildiz
                                   TRT Türk
                                   TRT World
                                   TürkHaber
                                   TV 1
                                   TV4
                                   TV 8
                                   TV 24
                                   TV 41
                                   TV 42
                                   TV 52
                                   TV 100
                                   TV 264
                                   TV Den
                                   TVNET
                                   Ulke TV
                                   Urfa Natik TV
                                   Van 65 TV
                                   Vav TV
                                   Viasat History (TR)
                                   Zarok TV
                                   beIN Movies Stars
                                   beIN Movies Turk
                                   beIN Box Office 1
                                   beIN Box Office 2
                                   beIN Box Office 3
                                   Alvin Channel TV
                                   AnewZ
                                   APA TV
                                   ARB
                                   ARB 24
                                   ARB Gunes
                                   Ayaz TV
                                   Az TV
                                   Azstar TV
                                   Baku TV
                                   CBC Sport (AZ)
                                   Dunya TV
                                   EL TV
                                   Ictimai TV
                                   Kanal 35
                                   Kanal S
                                   Kapaz TV
                                   MCJ TV
                                   Medeniyyet TV
                                   MTV (Azerbaijan)
                                   Naxcivan TV
                                   Space TV
                                   TMB TV
                                   Vilayet TV
                                   VIP TV
                                   Xezer TV
                                   KN Music TV
                                   B4U Movies
                                   B4U Kadak
                                   Bhojpuri Cinema
                                   Dangal TV
                                   Dangal 2
                                   Goldmines Movies
                                   Goldmines 2
                                   Jalsha Movies HD
                                   Manoranjan Movies
                                   Manoranjan TV
                                   Manoranjan Prime
                                   Max Movies
                                   Movies Now HD
                                   Roja Movies
                                   Shubh Cinema TV
                                   The Movie Club
                                   The Movie Club +2
                                   Zee Bollymovies
                                   Zee Bollymovies Australia
                                   Zee Cine Classic
                                   Zee Cinema APAC
                                   Zee Cinemalu HD
                                   Zee Classic
                                   Zee Horror Nights
                                   Zee South Flix
                                   Colors Cineplex Bollywood
                                   Indywood TV
                                   Epic Bharat
                                   9X Jalwa
                                   9X Jhakaas
                                   9X Tashan
                                   9XM
                                   B4U Hitz
                                   ETV Music
                                   Max Music
                                   PTC Music
                                   PTC Punjabi Gold
                                   Punjabi Hits
                                   Raj Musix Kannada
                                   Sangeet Bhojpuri
                                   Songdew TV
                                   Steelbird Music
                                   Tarang Music
                                   Tunes 6
                                   YRF Music
                                   ZB Music
                                   Epic Music
                                   MTV (US)
                                   VH1
                                   CMT
                                   BET
                                   Fuse TV
                                   MTV UK
                                   Kiss TV
                                   The Box
                                   Magic Music
                                   4Music
                                   MCM
                                   Trace France
                                   NRJ 12
                                   MTV Germany
                                   VIVA
                                   1LIVE
                                   MTV Italia
                                   Radio Italia TV
                                   Deejay TV
                                   Sol Música
                                   MTV Spain
                                   Los 40 TV
                                   MUZ-TV
                                   RU.TV
                                   MTV Russia
                                   Zoom (India, music)
                                   Mastiii
                                   MTV India
                                   Multishow
                                   MTV Brasil
                                   MTV Latinoamérica
                                   HTV
                                   Ritmoson Latino
                                   Rotana Music
                                   Mazzika
                                   Melody Hits
                                   El Mehwar Music
                                   Nogoum FM
                                   Channel V China
                                   MTV China
                                   Mnet
                                   M2
                                   Music On! TV
                                   Space Shower TV
                                   Trace Africa
                                   Channel O
                                   MTV Base Africa
                                   MTV Asia
                                   Channel V International
                                   Trace Urban
                                   Trace Toca
                                   CNN International
                                   BBC World News
                                   Al Jazeera English
                                   France 24 (English)
                                   DW News
                                   Sky News International
                                   Euronews
                                   RT International
                                   CGTN
                                   CNN
                                   Fox News
                                   MSNBC
                                   ABC News Live
                                   CBS News
                                   NBC News NOW
                                   Bloomberg TV
                                   Newsmax
                                   C-SPAN
                                   BBC News
                                   Sky News
                                   GB News
                                   ITV News
                                   France 24
                                   BFM TV
                                   LCI
                                   DW (Deutsche Welle)
                                   Tagesschau24
                                   Welt (WELT)
                                   n-tv
                                   24 Horas (RTVE)
                                   Rai News 24
                                   SkyTG24
                                   Russia 24
                                   RT (Russia Today)
                                   CNN Türk
                                   Al Arabiya
                                   Al Ekhbariya
                                   Sky News Arabia
                                   Al Jazeera Arabic
                                   Al Qahera News
                                   ON News
                                   i24NEWS
                                   NDTV 24x7
                                   Republic TV
                                   India Today
                                   Times Now
                                   WION
                                   CNBC-TV18
                                   Zee News
                                   Aaj Tak
                                   CCTV News
                                   NHK World Japan
                                   Arirang TV
                                   YTN
                                   KBS World
                                   ABC News (Australia)
                                   Sky News Australia
                                   GloboNews
                                   CNN Brasil
                                   CNN en Español
                                   Telesur
                                   eNCA
                                   SABC News
                                   Channels TV
                                   Arise News
                                   Geo News
                                   ARY News
                                   CNBC International
                                   Bloomberg TV Europe
                                   """;

    private static readonly HashSet<string> NormalizedNames = BuildLookup();

    private static HashSet<string> BuildLookup()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        var lines = RawList.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            foreach (var alias in line.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var normalized = ChannelNameNormalizer.Normalize(alias);
                if (!string.IsNullOrEmpty(normalized))
                    result.Add(normalized);
            }
        }

        return result;
    }

    public static bool Contains(string channelName)
        => NormalizedNames.Contains(ChannelNameNormalizer.Normalize(channelName));
}

#region ChannelNameNormalizer

    public static partial class ChannelNameNormalizer
    {
        [GeneratedRegex(@"\b(hd|fhd|uhd|4k|sd|hevc|h265|backup|bk)\b", RegexOptions.IgnoreCase)]
        private static partial Regex QualitySuffixPattern();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespacePattern();

        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var normalized = name.Trim().ToLowerInvariant();
            normalized = QualitySuffixPattern().Replace(normalized, " ");
            normalized = WhitespacePattern().Replace(normalized, " ").Trim();

            return normalized;
        }
    }

    #endregion
