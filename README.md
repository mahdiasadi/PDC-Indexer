# PDC Indexer - Professional Disk Catalog Indexer

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![License](https://img.shields.io/badge/license-MIT-blue)]()
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)]()
[!.NET](https://img.shields.io/badge/.NET-9.0-purple)]

A high-performance, multi-threaded file indexing and search application for Windows. Uses NTFS MFT (Master File Table) parsing for near-instant indexing of entire drives, with SQLite/FastIndex persistence, archive support, and multi-language UI.

---

# PDC Indexer - ایندکس‌گر حرفه‌ای کاتالوگ دیسک

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![License](https://img.shields.io/badge/license-MIT-blue)]()
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)]()
[!.NET](https://img.shields.io/badge/.NET-9.0-purple)]

برنامه جستجو و ایندکس‌سازی پرسرعت و چند‌نخی برای ویندوز. از پردازش MFT (جدول اصلی فایل) NTFS برای ایندکس‌گیری تقریباً لحظه‌ای کل درایوها استفاده می‌کند، با پشتیبانی از پایگاه داده SQLite/FastIndex، آرشیو و رابط کاربری چندزبانه.

---

## ✨ Features / امکانات

### 🚀 Ultra-Fast Indexing / ایندکس‌گیری فوق‌العاده سریع
- **NTFS MFT Parsing**: Direct parsing of Master File Table for maximum speed
- **Multi-threaded**: Utilizes all CPU cores for parallel processing
- **Incremental Updates**: USN Journal support for real-time change detection
- **Smart Path Reconstruction**: Efficient FRN-to-path resolution algorithm

### 🔍 Powerful Search / جستجوی قدرتمند
- **Multiple Search Modes**: Prefix, Contains, Wildcard, Exact, Regex
- **Real-time Results**: Live filtering as you type
- **Multi-drive Search**: Search across all indexed drives simultaneously
- **Archive Search**: Search within archived indexes

### 💾 Persistence & Archives / پایداری و آرشیو
- **FastIndex Format**: Custom binary format for instant loading (10x faster than SQLite)
- **SQLite Backend**: Reliable ACID-compliant storage
- **Archive Management**: Create, load, merge, and search historical snapshots
- **Auto-save**: Periodic background saves during long indexing operations

### 🌍 Multi-language Support / پشتیبانی چندزبانه
| Language / زبان | Code / کد | Status / وضعیت |
|---|---|---|
| English / انگلیسی | `en` | ✅ Complete |
| فارسی / Persian | `fa` | ✅ Complete |
| العربية / Arabic | `ar` | ✅ Complete |
| Türkçe / Turkish | `tr` | ✅ Complete |

### 🖥️ Modern WPF UI / رابط کاربری WPF مدرن
- Dark/Light theme support
- Virtualized lists for millions of entries
- Real-time progress reporting
- Keyboard shortcuts
- Context menus for file operations

---

## 📋 Requirements / پیش‌نیازها

- **OS**: Windows 10/11 (64-bit)
- **Runtime**: .NET 9.0 Desktop Runtime
- **Privileges**: Administrator required for NTFS MFT access
- **Disk**: NTFS formatted drives for full-speed indexing

---

## 🚀 Quick Start / راهنمای سریع

### Installation / نصب
```bash
# Clone repository
git clone https://github.com/mahdiasadi/PDC-Indexer.git
cd PDC-Indexer

# Build
dotnet build -c Release

# Run
dotnet run --project src/ProjectIndexer.Wpf
```

### First Run / اولین اجرا
1. **Run as Administrator** (required for MFT access)
2. Select drives to index from the drive list
3. Click **"Index All Drives"** / **"ایندکس تمام درایوها"**
4. Wait for indexing to complete
5. Start searching immediately!

---

## ⌨️ Keyboard Shortcuts / میانبرهای کیبورد

| Shortcut | Action / عملیات |
|---|---|
| `Ctrl+F` | Focus search box / فوکوس روی جستجو |
| `Ctrl+I` | Start indexing / شروع ایندکس |
| `Ctrl+L` | Load from database / بارگذاری از دیتابیس |
| `Ctrl+Shift+A` | Show archives / نمایش آرشیوها |
| `Enter` | Open selected file / باز کردن فایل انتخابی |
| `Ctrl+C` | Copy path / کپی مسیر |
| `Ctrl+Shift+C` | Copy name / کپی نام |

---

## 🏗️ Architecture / معماری

```
PDC-Indexer/
├── src/
│   ├── ProjectIndexer.Core/          # Core indexing engine
│   │   ├── Mft/                      # NTFS MFT Parser
│   │   ├── Indexing/                 # FastIndex, InMemoryIndex, Trie
│   │   ├── Archiving/                # FastArchiveIndex, ArchiveManager
│   │   ├── Database/                 # SQLite IndexDatabase
│   │   ├── FileSystem/               # Providers (NTFS, FAT, SMB)
│   │   ├── Native/                   # Win32 P/Invoke
│   │   └── Searching/                # SearchEngine
│   ├── ProjectIndexer.Wpf/           # WPF Application
│   │   ├── ViewModels/               # MVVM ViewModels
│   │   ├── Resources/                # Localization (.resx)
│   │   └── Collections/              # Virtualized collections
│   └── ProjectIndexer.Console/       # CLI tool
└── tests/
    └── ProjectIndexer.Core.Tests/    # Unit & Integration tests
```

### Key Components / کامپوننت‌های کلیدی

| Component | Description / توضیح |
|---|---|
| `MftParser` | Parallel NTFS MFT record parser with fixup support |
| `FastIndex` | Memory-mapped binary index with Trie + n-gram search |
| `InMemoryIndex` | Thread-safe in-memory search structures |
| `ArchiveManager` | Archive lifecycle (create, load, search, merge) |
| `IndexEngine` | Coordinates providers, indexes, and persistence |
| `SearchEngine` | Advanced query parser (prefix, wildcard, regex, field filters) |

---

## 🔧 Configuration / تنظیمات

### appsettings.json
```json
{
  "DatabaseSettings": {
    "DatabaseFolder": "%LOCALAPPDATA%\\ProjectIndexer"
  },
  "Language": "en"
}
```

### Supported Languages / زبان‌های پشتیبانی شده
Change language in Settings or edit `appsettings.json`:
```json
"Language": "fa"  // Persian
"Language": "ar"  // Arabic
"Language": "tr"  // Turkish
```

---

## 📊 Performance / عملکرد

| Operation / عملیات | Typical Time / زمان معمولی |
|---|---|
| Full Drive Index (1TB, 2M files) | 30-60 seconds |
| FastIndex Load (2M entries) | < 500ms |
| SQLite Load (2M entries) | 3-5 seconds |
| Search (Prefix) | < 1ms |
| Search (Contains/Regex) | 10-50ms |
| Archive Create (2M entries) | 2-3 seconds |
| Archive Load | < 200ms |

---

## 🧪 Testing / تست

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MftParser"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 🤝 Contributing / مشارکت

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

### Development Guidelines / راهنماهای توسعه
- Follow existing code style
- Add tests for new features
- Update localization files for UI changes
- Run `dotnet format` before committing

---



## 🙏 Acknowledgments / تشکر

- **NTFS Documentation**: Microsoft & community reverse-engineering efforts
- **CommunityToolkit.Mvvm**: Modern MVVM library
- **SQLite**: Embedded database engine
- **Contributors**: All who reported issues and submitted PRs

---

## 📞 Support / پشتیبانی

- **Issues**: [GitHub Issues](https://github.com/mahdiasadi/PDC-Indexer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mahdiasadi/PDC-Indexer/discussions)
- **Email**: sahagroup@gmail.com, mahdiasadi@yahoo.com

---

<div align="center">

For contact me: sahagroup@gmail.com, mahdiasadi@yahoo.com

</div>