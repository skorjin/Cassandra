# Архитектура

Слои и зависимости (стрелка означает «знает о»):

```
App ──► Presentation ──► Application ──► Domain ◄── Infrastructure
 └─────────────────────────────────────────────────────┘
```

`Domain` ни от чего не зависит. `Infrastructure` и `Presentation` не знают друг о друге. `App` — единственное место,
где всё собирается (DI).

## Поток данных

```
GdiScreenCapturer ─► FrameChangeDetector ─► TesseractOcrEngine ─► ChatTextDiffer ─► ChatLineParser ─► OnnxMarianTranslator ─► HudViewModel
   (кадр в RAM)        (изменился ли кадр?)    (текст строк)       (только новые)      (ник / текст)        (перевод)              (окно)
```

Оркеструет `TranslationPipeline`, цикл запускает `CaptureLoopService` в фоновом потоке.

- OCR запускается только когда кадр изменился и затем стабилизировался (чат анимируется).
- Если перевод не удался (например, ещё нет модели), строки считаются непросмотренными и переводятся позже.
- Кадры живут только в памяти. Окна приложения исключены из захвата (`WDA_EXCLUDEFROMCAPTURE`).

## Модели перевода

`IModelCatalog` отдаёт список вариантов (встроенный каталог, при наличии сети его заменяет свежий из репозитория).
`ModelManager` держит состояние, запускает загрузку и повторяет её при сбоях сети. `HttpModelDownloader` выполняет
один проход: докачивает файлы запросами Range, следит за зависанием, проверяет SHA-256. `ModelStorage` хранит файлы:

```
%LocalAppData%\CassandraData\models\{вариант}\
    v{версия}\            готовая модель (внутри маркер .ready)
    staging-v{версия}\    загрузка в процессе (*.part докачиваются)
```

Версия никогда не меняется после установки: обновление встаёт рядом, переводчик переключается на новый путь,
старый каталог удаляется. `OnnxMarianTranslator` загружает направление при первом использовании и выгружает
модели, когда активный каталог сменился.

## Обновления приложения

`IAppUpdater` (реализация `VelopackAppUpdater`) проверяет GitHub Releases. `Program.Main` вызывает
`VelopackApp.Build().Run()` до запуска WPF. В запуске из папки сборки обновления отключены.

## MVVM

Code-behind только там, где XAML не справляется: перетаскивание окон, нативные стили, фокус.

- `HudViewModel` — строки, пауза, вкладки, ящик настроек.
- `SettingsViewModel`, `ModelsViewModel` (+ `ModelOptionViewModel`), `UpdateViewModel`, `SourceEditorViewModel`,
  `ReplyViewModel`, `CaptureFrameViewModel`.
- `HotkeyCoordinator` связывает глобальные хоткеи с командами.
- События из фоновых потоков попадают в UI через `IUiDispatcher`.

## Тесты

| Проект | Что проверяет |
|---|---|
| `Cassandra.Application.Tests` | конвейер, детектор, diff, цикл, менеджер моделей (повторы, пауза, ошибки) |
| `Cassandra.Infrastructure.Tests` | загрузчик на локальном HTTP-сервере (обрыв, Range, зависание, битые данные), каталог, OCR, захват, Win32, реальный перевод |
| `Cassandra.Presentation.Tests` | ViewModels, меню моделей, обновления, хоткеи |
