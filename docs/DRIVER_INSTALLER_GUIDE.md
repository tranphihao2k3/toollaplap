# Huong Dan Su Dung Chuc Nang "Cai Driver"

## Muc Luc
1. [Tong Quan](#tong-quan)
2. [Cach Hoat Dong](#cach-hoat-dong)
3. [Cau Hinh driver_config.json](#cau-hinh-driver_configjson)
4. [Cach Lay Link Google Drive](#cach-lay-link-google-drive)
5. [Chuan Bi File Driver ZIP](#chuan-bi-file-driver-zip)
6. [Su Dung Trong App](#su-dung-trong-app)
7. [Xu Ly Loi Thuong Gap](#xu-ly-loi-thuong-gap)

---

## Tong Quan

Chuc nang **Cai Driver** cho phep:
- Tu dong phat hien model laptop qua WMI
- Match voi danh sach driver da cau hinh san
- Tai driver tu Google Drive, giai nen, va cai bang `DISM /Add-Driver`
- Ho tro nhieu model, moi model co danh sach driver rieng

## Cach Hoat Dong

```
Mo tab "Cai Driver"
    |
    v
App doc SysInfo.ModelName (tu WMI)
    |
    v
So sanh voi "ModelMatch" trong driver_config.json (case-insensitive, contains)
    |
    +-- Match --> Hien danh sach driver cho model do
    |
    +-- Khong match --> Hien "Khong tim thay driver cho model nay"

User chon driver --> Bam "CAI DRIVER"
    |
    v
Voi moi driver da chon:
    1. Download ZIP tu Google Drive
    2. Giai nen ZIP ra thu muc tam
    3. Chay: DISM /Online /Add-Driver /Driver:{path} /Recurse /ForceUnsigned
    4. Cap nhat trang thai (Thanh cong / That bai)
```

## Cau Hinh driver_config.json

File nam tai: `Resources/driver_config.json`

### Cau Truc

```json
[
  {
    "Model": "Ten day du cua model (de hien thi)",
    "ModelMatch": ["chuoi1", "chuoi2"],
    "Drivers": [
      {
        "Name": "Ten driver",
        "Category": "Loai (WiFi/Audio/GPU/LAN/Bluetooth/Chipset/...)",
        "FileName": "TenFile.zip",
        "DownloadUrl": "https://drive.google.com/file/d/FILE_ID/view",
        "Description": "Mo ta ngan"
      }
    ]
  }
]
```

### Giai Thich Cac Truong

| Truong | Mo ta | Vi du |
|--------|-------|-------|
| `Model` | Ten model day du, dung de hien thi tren UI | `"IdeaPad Gaming 3 15ARH7"` |
| `ModelMatch` | Mang cac chuoi de fuzzy match. Chi can model name **chua** bat ky chuoi nao la match | `["IdeaPad Gaming 3", "82SB"]` |
| `Name` | Ten hien thi cua driver | `"Intel WiFi Driver"` |
| `Category` | Phan loai driver (hien thi badge mau xanh) | `"WiFi"`, `"Audio"`, `"GPU"` |
| `FileName` | Ten file ZIP khi tai ve | `"WiFi_IdeaPad3.zip"` |
| `DownloadUrl` | Link Google Drive chua file ZIP | Link dang `/file/d/xxx/view` |
| `Description` | Mo ta chi tiet (hien duoi ten driver) | `"Intel AX201 WiFi 6"` |

### Vi Du Day Du

```json
[
  {
    "Model": "IdeaPad Gaming 3 15ARH7",
    "ModelMatch": ["IdeaPad Gaming 3", "82SB"],
    "Drivers": [
      {
        "Name": "Intel WiFi Driver",
        "Category": "WiFi",
        "FileName": "WiFi_IdeaPad3.zip",
        "DownloadUrl": "https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUvWxYz/view",
        "Description": "Intel AX201 WiFi 6"
      },
      {
        "Name": "Realtek Audio Driver",
        "Category": "Audio",
        "FileName": "Audio_IdeaPad3.zip",
        "DownloadUrl": "https://drive.google.com/file/d/2BcDeFgHiJkLmNoPqRsTuVwXyZa/view",
        "Description": "Realtek HD Audio"
      },
      {
        "Name": "NVIDIA GPU Driver",
        "Category": "GPU",
        "FileName": "GPU_IdeaPad3.zip",
        "DownloadUrl": "https://drive.google.com/file/d/3CdEfGhIjKlMnOpQrStUvWxYzAb/view",
        "Description": "NVIDIA GeForce RTX 3050"
      }
    ]
  },
  {
    "Model": "HP Pavilion 15-eg2xxx",
    "ModelMatch": ["Pavilion 15-eg", "HP Pavilion 15"],
    "Drivers": [
      {
        "Name": "Intel WiFi Driver",
        "Category": "WiFi",
        "FileName": "WiFi_HPPavilion15.zip",
        "DownloadUrl": "https://drive.google.com/file/d/4DeFgHiJkLmNoPqRsTuVwXyZaBc/view",
        "Description": "Intel AX201 WiFi 6"
      }
    ]
  }
]
```

### Cach Xac Dinh ModelMatch

De biet dien gi vao `ModelMatch`, chay lenh nay trong CMD/PowerShell:

```powershell
wmic computersystem get model
```

hoac:

```powershell
Get-CimInstance -ClassName Win32_ComputerSystem | Select-Object Model
```

Ket qua tra ve kieu: `IdeaPad Gaming 3 15ARH7` hoac `HP Pavilion 15-eg2056TU`

**Meo**: Dung nhieu chuoi ngan trong `ModelMatch` de tang kha nang match:
- `"IdeaPad Gaming 3"` - match ca dong IdeaPad Gaming 3
- `"82SB"` - match chinh xac model code
- Khong can viet hoa/thuong chinh xac (so sanh case-insensitive)

## Cach Lay Link Google Drive

### Buoc 1: Upload file ZIP len Google Drive

1. Vao [drive.google.com](https://drive.google.com)
2. Upload file driver ZIP

### Buoc 2: Chia se file

1. Click phai vao file --> **Chia se** (Share)
2. Doi quyen thanh **"Bat ky ai co duong lien ket"** (Anyone with the link)
3. Copy link

### Buoc 3: Dan link vao config

Link se co dang:
```
https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUvWxYz/view?usp=sharing
```

Dan nguyen link nay vao truong `DownloadUrl`. App se tu dong chuyen thanh link download truc tiep.

**Luu y**: File phai duoc chia se **cong khai** (Anyone with the link). Neu de Private thi khong tai duoc.

## Chuan Bi File Driver ZIP

### Yeu Cau

Moi file ZIP can chua **file .inf** (driver Windows). DISM chi nhan dang driver qua file `.inf`.

### Cau Truc Thu Muc Trong ZIP

```
WiFi_IdeaPad3.zip
  |
  +-- WiFi_Driver/
        |-- netrtwlane.inf
        |-- netrtwlane.sys
        |-- netrtwlane.cat
        |-- ... (cac file phu tro khac)
```

hoac co the co nhieu thu muc con:

```
Audio_IdeaPad3.zip
  |
  +-- Realtek_Audio/
        +-- HDXRT/
        |     |-- HDXRT.inf
        |     |-- ...
        +-- Extension/
              |-- RTExt.inf
              |-- ...
```

DISM se dung flag `/Recurse` de quet tat ca thu muc con tim file `.inf`.

### Cach Lay Driver INF

**Cach 1: Tu trang chu nha san xuat**
- Tai driver tu Lenovo/HP/Dell/ASUS support
- Nha san xuat thuong cung cap dang `.exe` installer
- Giai nen file `.exe` bang 7-Zip de lay folder chua `.inf`

**Cach 2: Tu may dang chay tot**
1. Mo Device Manager
2. Click phai vao thiet bi --> Properties --> Driver --> Driver Details
3. Ghi lai duong dan file `.inf` (thuong nam trong `C:\Windows\INF\oem*.inf`)
4. Export driver:
```powershell
# Export tat ca driver cua may
dism /online /export-driver /destination:C:\DriversBackup
```
5. Nen thu muc driver can thiet thanh `.zip`

**Cach 3: Tu DriverPack/Snappy Driver**
- Tai driver pack theo model tu cac nguon nhu DriverPack Solution
- Chon dung driver, nen thanh ZIP

## Su Dung Trong App

### Buoc 1: Mo tab Cai Driver
- Click **"Cai Driver"** tren thanh sidebar (ben trai)

### Buoc 2: Kiem tra model
- Phan header hien:
  - **Model phat hien**: ten model tu WMI
  - **Trang thai match**: da tim thay driver hay chua
  - Dau xanh = da match, dau do = khong match

### Buoc 3: Chon driver
- Tick checkbox tung driver can cai
- Hoac tick "Chon tat ca" o goc trai tren

### Buoc 4: Bam "CAI DRIVER"
- App se lan luot xu ly tung driver:
  - **Dang tai...** (tim) - Download tu Drive
  - **Giai nen...** - Extract ZIP
  - **Dang cai driver (DISM)...** (xanh duong) - DISM cai driver
  - **Hoan tat!** (xanh la) - Thanh cong
  - **That bai** (do) - Co loi

### Buoc 5: Kiem tra ket qua
- Thanh progress bar hien tien trinh tong
- Dong trang thai cuoi cung hien so luong thanh cong/that bai

## Xu Ly Loi Thuong Gap

### "Khong tim thay driver cho model nay"
- **Nguyen nhan**: ModelMatch trong config khong match voi ten model WMI
- **Cach xu ly**:
  1. Xem ten model hien thi trong app
  2. Them chuoi match phu hop vao `ModelMatch` trong `driver_config.json`
  3. Bam nut "Lam moi" trong app

### "Tai that bai"
- **Nguyen nhan**: Link Google Drive sai hoac file chua duoc chia se cong khai
- **Cach xu ly**:
  1. Kiem tra link Drive con hoat dong khong (mo bang trinh duyet)
  2. Dam bao file da chia se "Anyone with the link"
  3. Kiem tra ket noi mang

### "DISM that bai"
- **Nguyen nhan**: File ZIP khong chua file `.inf` hop le, hoac driver khong tuong thich
- **Cach xu ly**:
  1. Kiem tra file ZIP co chua file `.inf` khong
  2. Dam bao driver tuong thich voi phien ban Windows dang dung
  3. Thu chay thu cong trong CMD (Admin):
  ```
  DISM /Online /Add-Driver /Driver:"C:\path\to\driver\folder" /Recurse /ForceUnsigned
  ```
  4. Doc output DISM de biet loi cu the

### "Giai nen that bai"
- **Nguyen nhan**: File ZIP bi loi hoac dung dinh dang .rar/.7z nhung may chua cai 7-Zip
- **Cach xu ly**:
  1. Dung dinh dang `.zip` (duoc ho tro native)
  2. Neu dung `.rar` hoac `.7z`: cai 7-Zip truoc (`C:\Program Files\7-Zip\7z.exe`)

### App can quyen Admin
- Chuc nang DISM yeu cau quyen Administrator
- App LapLapAutoSetup da yeu cau quyen Admin khi khoi dong
- Neu gap loi "Access Denied": dam bao chay app bang "Run as Administrator"

---

## Tom Tat Quy Trinh Setup Nhanh

```
1. Xac dinh model laptop (wmic computersystem get model)
2. Chuan bi file driver ZIP (chua file .inf)
3. Upload len Google Drive, chia se cong khai
4. Sua driver_config.json: them model + link
5. Mo app --> Tab "Cai Driver" --> Chon driver --> Bam "CAI DRIVER"
```

## Vi Tri Cac File Lien Quan

| File | Duong Dan | Muc Dich |
|------|-----------|----------|
| Config | `Resources/driver_config.json` | Mapping model -> drivers |
| ViewModel | `ViewModels/DriverViewModel.cs` | Logic xu ly |
| View | `Views/DriverView.xaml` | Giao dien |
| Model | `Models/DriverItem.cs` | Data class driver |
| Model | `Models/DriverModelConfig.cs` | Data class config |
| Service | `Services/InstallService.cs` | Method `InstallDriverWithDismAsync` |
| Download | Thu muc tai ve: `%UserProfile%\Downloads\LapLapDownloads\` | File ZIP tai ve |
