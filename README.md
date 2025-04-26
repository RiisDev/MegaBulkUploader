# MegaBulkUploader

**MegaBulkUploader** is a powerful tool designed for bulk uploading files or directories to MEGA.nz using proxy accounts. It automatically generates temporary accounts and uploads data in parallel streams with configurable size limits.

> [!IMPORTANT]
> This was originally built upon Windows, and very loosely tested in linux, so I cannot guarantee support,
> I'd love for some Linux C# developers to make some PRs to get compatibility 100%

---

## 🚀 Features

- ✅ Auto-generates MEGA proxy accounts  
- 📂 Supports both file and directory uploads  
- 📦 Splits uploads based on file size or directory structure  
- 📝 Detailed logging in plain text or BBCode format  



![C#](https://img.shields.io/badge/-.NET%208.0-blueviolet?style=for-the-badge&logo=windows&logoColor=white)
[![Support Server](https://img.shields.io/discord/477201632204161025.svg?label=Discord&logo=Discord&colorB=7289da&style=for-the-badge)](https://discord.gg/yyuggrH)
![GitHub](https://img.shields.io/github/license/RiisDev/MegaBulkUploader?style=for-the-badge)
![Build](https://img.shields.io/github/actions/workflow/status/RiisDev/MegaBulkUploader/dotnet.yml?style=for-the-badge)

---

## 🛠 Usage

```bash
dotnet MegaBulkUploader.dll <pathToUpload> [options]
```

### Arguments

| Argument              | Description                                                 |
|-----------------------|-------------------------------------------------------------|
| `pathToUpload`        | **Required.** Path to the file or directory to upload       |

### Options

| Option                     | Alias | Description                                                                 |
|----------------------------|-------|-----------------------------------------------------------------------------|
| `--help`                   | `-h`  | Show help message and exit                                                  |
| `--start-index <n>`        | `-si` | Starting section index (default: 0)                                         |
| `--max-size <bytes>`       | `-ms` | Max size (in bytes) per chunk (default: `19327352832` equal to ~20GB)       |
| `--upload-streams <n>`     | `-us` | Number of parallel upload streams (default: 6, min: 1, max: 6)             |
| `--output <file>`          | `-o`  | Output log file (default: `Upload.log`)                                    |
| `--bbcode`                 | `-bb` | Output logs in BBCode format (default: `false`)                            |
| `--bbcode-out <file>`      | `-bo` | BBCode output log file (default: `BbUpload.log`)                           |

---

## 💡 Example

```bash
dotnet MegaBulkUploader.dll ./my-folder --start-index 0 --max-size 19327352832 --upload-streams 6 --output Upload.log
```

---

## ⚙️ Requirements

- [.NET 8.0+ SDK](https://dotnet.microsoft.com/)
- On Linux: Run as `sudo` for proper permissions
    - MegaCLI must be downloaded and located in `/usr/bin/`
- On Windows make sure that `\cli\x64` or `\cli\x86` depening on architecture exists with all the required files

---

## 🏗️ Compiling from Source

To build and publish the project locally, follow these steps:

### 1. Clone the repository:

```bash
git clone https://github.com/RiisDev/MegaBulkUploader.git
cd MegaBulkUploader
```

### 2. Publish using the built-in **FolderPublish** profile:

```bash
dotnet publish -c Release -p:PublishProfile=FolderPublish
```

> The output will be located under `bin/Publish`

You can then run the application using:

```bash
dotnet MegaBulkUploader.dll <pathToUpload> [options]
```

---

## 📁 Logs

- **Standard Log:** `Upload.log` (default or custom via `--output`)  
- **BBCode Log:** `BbUpload.log` (when `--bbcode` is enabled, and customized via `--bbcode-out`)

---

## 📌 Notes

- This tool runs indefinitely and listens for cancellation via CTRL+C.  
- For Linux users, make sure MegaCli is installed into the `/usr/bin/` directory for support and execute via sudo.
- For Windows Users, make sure the cli folder is present in the dll directory.
- BBCode output is useful for forums or formatted sharing platforms.

---

## 📄 License

MIT License — feel free to use, modify, and distribute.

---

## ⚠️ Disclaimer

This project is **not affiliated with, endorsed by, or associated with [MEGA.nz](https://mega.nz/)** in any way.  
It is a **personal hobby project** created for educational purposes.

Use of this tool is at your own risk. By using it, you acknowledge that:

- You are responsible for complying with MEGA’s [Terms of Service](https://mega.nz/terms).
- The author assumes **no responsibility** for misuse, abuse, or any consequences (including account bans).
- This software is provided **as-is**, without any warranties or guarantees of uptime, functionality, or support.

If you are unsure about what is permitted, please refer to MEGA's official documentation and policies.

---