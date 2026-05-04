# Печать и PDF-экспорт — уроки и подводные камни

Документ-памятка по тому, как устроен рендер XAML-вьюх в bitmap для печати/PDF-экспорта в этом проекте, и какие WPF-квирки приводили к багам. Прежде чем менять что-либо в `BulkBracketPdfExporter.cs` или `VisualPrinter.cs` — прочитай этот файл целиком.

## Что у нас есть

| Компонент | Назначение | Файл |
|---|---|---|
| `VisualPrinter.PrintAcrossPages` | Одиночная печать одной XAML-вьюхи на принтер. Off-tree рендер → bitmap → разбивка на страницы → отправка в очередь печати. | `Wrestling.UI.Utils/VisualPrinter.cs` |
| `BulkBracketPdfExporter.ExportAsync` | Массовый экспорт всех протоколов в `.pdf` файлы. Каждая группа → один PDF. Запускается с дашборда кнопкой «Скачать сетки PDF». | `Wrestling.UI.Material/Tournament/Print/BulkBracketPdfExporter.cs` |

Обе утилиты используют `RenderTargetBitmap` для растеризации XAML и затем нарезают результат на страницы A4. PDF-экспорт сериализует страницы через PdfSharp; принтер — через `FixedDocument`.

## Главный принцип: рендерить **off-tree**, без Window

**Правильно:**

```csharp
view.Width = imageableWidthDip;
view.Measure(new Size(imageableWidthDip, double.PositiveInfinity));
view.Measure(new Size(imageableWidthDip, double.PositiveInfinity));
var measuredHeight = Math.Max(view.DesiredSize.Height, 1);
view.Arrange(new Rect(0, 0, imageableWidthDip, measuredHeight));
view.UpdateLayout();
measuredHeight = Math.Max(measuredHeight, view.DesiredSize.Height);
measuredHeight = Math.Max(measuredHeight, view.ActualHeight);

var bitmap = new RenderTargetBitmap(/* width, measuredHeight, dpi, dpi, Pbgra32 */);
bitmap.Render(view);
```

**Неправильно** — хостить view в `Window` (даже скрытом, даже с `Top=-32000`):

- WPF режет визуальное дерево любого rooted Window до высоты **рабочей области экрана** (`SystemParameters.WorkArea.Height` в DIP). На FullHD-мониторе это ≈881 DIP.
- Любой dispatcher pump после Show() запускает layout-проход, который пере-Arrange'ит content в эту капнутую высоту.
- После этого даже если детачнуть view от Window, его `DesiredSize` остаётся капнутым — кеш сохраняется во внутреннем состоянии визуального дерева.
- Симптом: PDF выглядит как «верхняя половина протокола, дальше пусто», или таблица обрывается посередине строки, а на второй странице — белый лист.

`AllowsTransparency=true` усугубляет проблему (layered window), но и обычный Window страдает от того же лимита. SizeToContent.Height тоже капается экраном явным образом.

## Условия, при которых off-tree работает

ListView / ItemsControl материализуются off-tree **только** если у них отключена виртуализация:

```xml
<ListView VirtualizingPanel.IsVirtualizing="False"
          ScrollViewer.CanContentScroll="False">
```

Без этого `IsVirtualizing="False"` items не реализуются (нет Loaded events без PresentationSource), и DesiredSize вернёт высоту только видимого окна. Все вьюхи в `Tournament/Print/**` уже настроены правильно — при добавлении новой вьюхи в bulk-экспорт обязательно проставь эти атрибуты на всех ItemsControl/ListView.

## Подводные камни Measure

### 1. Width нельзя оставлять `infinity`

```csharp
// НЕЛЬЗЯ — content схлопнется в свою «естественную» узкую ширину
view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

// НАДО — фиксируем ширину страницы, высоту оставляем безграничной
view.Measure(new Size(imageableWidthDip, double.PositiveInfinity));
```

Если width = infinity, StackPanel/Grid measurer вычислит min-content width (текст без переносов, узкие колонки) и отдаст соответствующую короткую высоту. Bitmap получится крошечным.

### 2. Один Measure-проход недостаточен

Для сложных вьюх (`PrintBracketView` с вложенными ItemsControl + конвертерами высоты ячеек) нужно **два** Measure-прохода:
- Первый: реализует ItemsControl-контейнеры, инициирует генерацию контейнеров для items.
- Второй: уже видит реализованные контейнеры и считает корректные DesiredSizes.

### 3. После Arrange + UpdateLayout высота может ВЫРАСТИ

Конвертер `BracketCellLineHeightConverter` зависит от `match.RoundNumber` и `group.Bracket`. Эти данные доступны сразу, но layout-проход через бракет с RowDefinition.Height из MultiBinding отрабатывает в **несколько** циклов: первый Arrange задаёт начальные размеры ячеек, после чего конвертеры пересчитываются и ячейки растут. Поэтому:

```csharp
// Берём максимум из всех известных «правильных» высот
measuredHeight = Math.Max(measuredHeight, view.DesiredSize.Height);
measuredHeight = Math.Max(measuredHeight, view.ActualHeight);
```

Без этого `Math.Max` битмап рассчитывается по DesiredSize до Arrange, а финальный визуал имеет ActualHeight больше — куски бракета обрезаются. Это **критически важная** строчка, не убирать при рефакторинге.

## Threading

`RenderTargetBitmap.Render` требует STA. `BulkBracketPdfExporter` запускает выделенный STA-Thread через `new Thread().SetApartmentState(STA)`, а не `Task.Run`/`ThreadPool` (пул не STA). В finally:

```csharp
Dispatcher.CurrentDispatcher.InvokeShutdown();
```

— чтобы dispatcher, неявно созданный любым WPF-обращением, корректно остановился и thread не завис.

`view.Width = ...` и `Measure/Arrange/UpdateLayout` — всё на этом render-треде. **DataContext-свойства**, на которые биндится view (Tournament, Group, Wrestlers), создаются на UI-треде; сами объекты read-only через биндинги это переживают, но любая мутация через ObservableCollection с render-треда упадёт. Поэтому в `BuildExportJobs` все вычисления (`processor.Load`, `GetResults`, `teamCalculator.GetTeamResults`) делаются на UI-треде заранее, а ViewFactory лишь конструирует ViewModel и View из уже готовых данных.

## Разбивка на страницы

`FindCleanBreakRow` ищет «чистую» (low-variance) строку пикселей в нижней 40% части потенциальной страницы — чтобы не разорвать строку таблицы или ячейку бракета. Возвращает `bottomLine`, если ничего чистого не нашлось (тогда страница режется хирургически по краю — лучше так, чем зацикливаться на нулевой высоте).

Порог `deviationThreshold = 1500.0` подобран под текущие шрифты/контрастность. Если меняешь FontSize или цвет фона — пересмотри значение, иначе разбивка может попадать на середину строки или, наоборот, всегда уезжать к самому низу.

## RenderTargetBitmap имеет практический лимит ≈ 16K пикселей

WPF `RenderTargetBitmap` опирается на DirectX render targets, которые на большинстве GPU **молча обрезаются на ~16384 пикселях** в любом измерении. При DPI=300 и view высотой ~10K DIP получается 31K-пиксельный битмап → DirectX рендерит только первые ~16K пикселей, остальное остаётся прозрачным/чёрным. В PDF это превращается в «контент обрывается на странице N, footer на странице N+2, между ними пустота».

Поэтому `BulkBracketPdfExporter.RenderDpi` зафиксирован на **150 DPI**, а не 300. При 150 DPI 16K пикселей = ~10K DIP, чего хватает на самые длинные «Личные результаты» в реальных турнирах. Если вью становится ещё длиннее — нужно либо снижать DPI до 96, либо рендерить по страницам отдельными битмапами.

Симптом, по которому это диагностируется: контент рендерится нормально до какой-то страницы, потом **подряд** идут пустые страницы, и в самом конце оказывается footer (если он входит в видимую часть битмапа). DesiredSize/ActualHeight в логах при этом «правильные» — WPF говорит, что вью большая, но рендеринг отрезает.

## ListView column headers и MaterialDesign

Дефолтный стиль `GridViewColumnHeader` от MaterialDesignThemes добавляет **большой Padding** (~12 DIP с каждой стороны) и крупный шрифт. В узкой колонке (50–60 DIP) это съедает всё место под текст — заголовок виден внутри столбца как пустая полоса.

Решение — определить компактный стиль и привязать его через `GridView.ColumnHeaderContainerStyle`:

```xml
<Style x:Key="CompactColumnHeader" TargetType="GridViewColumnHeader">
    <Setter Property="Padding" Value="2,0,2,0" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
</Style>
...
<GridView ColumnHeaderContainerStyle="{StaticResource CompactColumnHeader}">
    ...
</GridView>
```

Применено в `PrintPersonalResultsView.xaml` и `PrintOlympicTeamResultsView.xaml`.

## Rotated headers (LayoutTransform RotateTransform) ненадёжны off-tree

В обычной on-screen ListView повёрнутые на -90° заголовки колонок («Командных баллов» вертикально) рендерятся нормально. Но в off-tree режиме без Window их визуальный выхлоп иногда теряется (видна пустая колонка с правильной шириной, а текста в заголовке нет). Похоже, GridViewColumnHeader клипует контент по своим границам, не пересчитанным под повёрнутый дочерний TextBlock.

В этих вьюхах **используем плоский короткий текст** для узких колонок («Разряд», «Поб.», «Пор.», «Классиф.», «Команд.», «1-е место» и т.п.) вместо `Style="{StaticResource RotatedText}"`. С компактным стилем заголовка текст влезает.

## Image overlay в footer: правильная структура

Чтобы наложить картинку «печать + подписи» поверх футера протокола, **не клади Image внутрь Grid с `RowSpan=2` и `Height=150`** — Grid распределит эту высоту между двумя строками текста, ячейки растут, текст разъезжается.

Правильная структура — outer-wrapper Grid с одной ячейкой, внутри которой два sibling'а:

```xml
<Grid Margin="0,5,0,5">
    <Grid VerticalAlignment="Center">  <!-- Inner: текст -->
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />  <!-- лейбл "Главный судья" -->
            <ColumnDefinition Width="*" />     <!-- спейсер -->
            <ColumnDefinition Width="Auto" />  <!-- ФИО (короткое через ShortName конвертер) -->
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>
        ...текстовые TextBlock'и...
    </Grid>

    <!-- Stamp поверх. Декларирован после inner Grid → выше z-order. -->
    <Image Source="{Binding ..., Converter={StaticResource OptionalImage}}"
           Stretch="Uniform"
           Height="150"
           HorizontalAlignment="Center" VerticalAlignment="Top"
           Margin="0,30,0,0"
           IsHitTestVisible="False" />
</Grid>
```

Inner Grid `VerticalAlignment="Center"` — текст центрируется в общей высоте outer Grid'а (которая определяется image+margin). Image `VerticalAlignment="Top" + Margin="0,30,0,0"` — стамп визуально опущен на ~30 DIP вниз для эстетики.

**Важно**: Image сам по себе вкладывается в layout. С `Height="150"` и `Margin="0,30,0,0"` outer Grid становится **180 DIP**, плюс свой `Margin` (например `"0,5,0,5"`). Если протокол на грани переполнения страницы (типичный случай — круговая на 5 участников), эти ~30 DIP image-margin'а могут стать причиной выхода на 2-ю страницу. Тогда:

1. Прижимай footer outer Grid Margin (`"0,30,0,10"` → `"0,5,0,5"` экономит 30 DIP).
2. Прижимай Margin у предыдущей секции (wrestlers list `"0,20,0,0"` → `"0,10,0,0"` экономит 10 DIP).
3. Реальный размер «печать + подписи» 150 DIP при 150 DPI рендера = 25 мм при 300 DPI печати ≈ 40 мм диаметр стампа. Не уменьшай ниже 130 DIP без необходимости.

## OptionalImagePathConverter: относительный путь

Картинка-печать копируется в папку `Images/` приложения (тот же паттерн, что `EmblemPath`), а в `GlobalSettings.SignatureFooterImagePath` сохраняется только имя файла. Конвертер `OptionalImagePathConverter`:

- пустой/null путь → `null` (Image не рендерится)
- абсолютный путь, файл существует → `BitmapImage`
- относительное имя → резолв через `{AppDir}/Images/{filename}`, если файл существует
- неподдерживаемое расширение или нет файла → `null`

`BitmapCacheOption.OnLoad` + `Freeze()` обязательны: без них исходный файл лочится до GC, что мешает пользователю переписать стамп.

## Прямая печать (без PDF) через VisualPrinter

Когда нужно отправить XAML-вьюху сразу на принтер (а не сохранять PDF) — используй существующий `VisualPrinter.PrintAcrossPages`, а не «сгенерируй PDF → открой через `Process.Start` с verb=print». Канонический паттерн (см. `Tournament/Print/PrintView.xaml.cs:15-25`):

```csharp
var dlg = new PrintDialog();
if (dlg.ShowDialog() == true)
{
    if (!VisualPrinter.PrintAcrossPages(dlg, view, "Печать"))
        MessageBox.Show("Ошибка печати. Попробуйте еще раз.");
}
```

`PrintDialog.ShowDialog()` сам показывает выбор принтера + настройки страницы. `VisualPrinter` рендерит off-tree (см. секцию выше) и отправляет в очередь. Никаких temp-файлов, никаких внешних PDF-ридеров.

## `documentName` для PrintDialog должен быть литералом без переменной кириллицы

Имя задания, которое мы передаём в `VisualPrinter.PrintAcrossPages(dlg, view, jobName)`, в итоге попадает в `PrintDialog.PrintDocument(paginator, jobName)`. **Microsoft Print to PDF (и, вероятно, часть других драйверов) портит кириллицу в этом поле**: строка приходит в title bar PDF-вьювера как UTF-8-байты, прочитанные как Latin-1 (`Расписание - Ковер` → `Ð€Ð°Ñ†Ð¸Ñ†Ð°Ð½Ð¸Ðµ - ÐıÐ¾Ð²ÐµÑ•`). Само содержимое PDF (рендер XAML через `RenderTargetBitmap`) от этого не страдает — кириллица там нормальная.

Решение — передавать **фиксированную короткую строку** того же стиля, что в `PrintView.xaml.cs` («Печать»). Не интерполируй имя ковра/группы/чего угодно прямо в `documentName`. Контекст (какой ковёр, какая группа) живёт в самом теле PDF через биндинги (`Stat.CarpetLabel`, `SelectedGroup.Name` и т.п.) — этого достаточно.

Менее очевидная альтернатива — транслитерировать кириллицу в латиницу — выглядит как «костыль» и захламляет код таблицей `а→a, б→b, …`. Если данные действительно нужны в имени задания, лучше сначала проверь, что они там вообще видны (физический принтер обычно отображает имя в очереди печати, а PDF-драйвер — в title bar полученного файла).

## `SharedSizeGroup` несовместим с `Width="<пиксели>"` на `ColumnDefinition`

Если у `ColumnDefinition` явная пиксельная ширина (`Width="120"`) и одновременно `SharedSizeGroup="Foo"` — WPF под капотом ведёт колонку как `Auto` (сжимается к самому широкому контенту), а не как 120 пикселей. Симптом: HorizontalAlignment="Center" на содержимом колонки визуально не центрирует, потому что центрировать не в чем — колонка ровно по ширине контента.

```xml
<!-- ❌ неправильно: WPF трактует как Auto, 120 игнорируется -->
<ColumnDefinition Width="120" SharedSizeGroup="ColTeams" />

<!-- ✅ правильно: либо Auto + SharedSize -->
<ColumnDefinition Width="Auto" SharedSizeGroup="ColTeams" />

<!-- ✅ правильно: либо явные пиксели без SharedSize -->
<ColumnDefinition Width="120" />
```

Колонки с одинаковой явной пиксельной шириной в разных Grid'ах (header + строки данных) выровняются и без `SharedSizeGroup` — за счёт совпадения чисел. `SharedSizeGroup` нужен **только** для синхронизации `Auto`-колонок поперёк нескольких Grid-областей в одной `Grid.IsSharedSizeScope="True"`.

## `TextAlignment="Center"` ≠ `HorizontalAlignment="Center"` в табличных print-вьюхах

Для надёжного центрирования текста по центру колонки в print-таблицах (`ItemsControl` + `Grid` per-row) используй `TextAlignment="Center"`, а не `HorizontalAlignment="Center"`.

- `HorizontalAlignment="Center"` сжимает TextBlock до натуральной ширины контента и центрирует **TextBlock** внутри родителя. В `StackPanel`-контейнере это срабатывает только если StackPanel сам растянут на всю ширину Grid-ячейки — что часто **не** так.
- `TextAlignment="Center"` центрирует **текст** внутри ширины TextBlock'а. TextBlock по умолчанию `HorizontalAlignment="Stretch"` в Grid-ячейке → занимает всю ширину колонки → текст центрирован относительно колонки.

Симптом «центрирование не работает»: заголовок выглядит сдвинутым к левому краю / стартует в той же x-координате, что более широкое содержимое строки данных. Замена `HorizontalAlignment="Center"` → `TextAlignment="Center"` чинит мгновенно.

## Star-sized колонки в print-таблицах: ItemsControl + Grid, не ListView + GridView

`GridViewColumn` **не поддерживает** `Width="*"` нативно. Если в print-таблице нужна одна колонка-«заполнитель» (например «Баллы» — пустое место под рукописные оценки), а остальные — фиксированные пиксели:

- ❌ `ListView` + `GridView` со `<GridViewColumn Width="???">` — звёздочка не работает, биндинг к `ActualWidth` родителя через конвертер хрупок.
- ✅ `ItemsControl` с `DataTemplate`, в котором каждая строка — `Grid` с явными `ColumnDefinitions` (mix фикс-пикселей и `*`). Шапку выкладывай отдельным Grid'ом сверху с теми же `ColumnDefinitions`.

Пример — `PrintScheduleView.xaml`. Заголовки и строки в двух разных Grid'ах с идентичными `ColumnDefinitions` (`40 / 150 / * / 120 / Auto / 40`) — выравниваются за счёт совпадения чисел.

## Чек-лист при добавлении новой XAML-вьюхи в bulk-экспорт

## Чек-лист при добавлении новой XAML-вьюхи в bulk-экспорт

1. На корневом контейнере (обычно `StackPanel`) задать `Background="White"` — иначе будет прозрачный PNG поверх PDF-фона (визуально нормально, но тяжелее файл).
2. На каждом `ListView`/`ItemsControl`/`DataGrid` поставить `VirtualizingPanel.IsVirtualizing="False"` и `ScrollViewer.CanContentScroll="False"`.
3. Никаких `Height="*"` или `MinHeight` в bottom-секциях — high stretch ломает измерение.
4. Если вьюха использует `ElementName` биндинги — корневой контейнер должен иметь `x:Name`, а биндинги должны разрешаться без PresentationSource (off-tree).
5. Подписи / footer'ы — отдельным `Grid` внизу `StackPanel`, чтобы `FindCleanBreakRow` мог найти чистую строку перед ними.
6. Если в ListView есть узкие колонки с заголовками — привяжи `ColumnHeaderContainerStyle="{StaticResource CompactColumnHeader}"`, иначе MaterialDesign-стиль съест всё место под текст.
7. Image-overlay для печати/подписей в footer — оборачивай в outer-wrapper Grid (см. секцию выше). Не клади Image внутрь общего Grid'а с `RowSpan=2` — растащит layout строк текста.
8. Прикинь общую высоту вью при минимальных данных (1–2 группы, 5 участников). Если на грани 697.7 DIP (A4 landscape imageable height) — ужми margin'ы у footer'а и таблицы.

## История

- 2026-04-28 → 2026-04-29: первая попытка bulk-экспорта через off-screen Window — рендер обрезался по высоте экрана. Дебаг через `view.DesiredSize` / `view.ActualHeight` показал, что layered window капается screen working-area. Решение — полностью off-tree, плюс `IsVirtualizing="False"` на ListView и `Math.Max(DesiredSize, ActualHeight)` после Arrange/UpdateLayout.
- 2026-04-29: добавили image-overlay «печать + подписи» в footer всех трёх print-вью. Прошли цикл «outer Grid с Image как sibling inner Grid'а» → много итераций по позиционированию и размеру стампа (40 мм круглой печати = 150 DIP при 150 DPI рендера). Ушли от RotatedText в headers — на узких колонках они рендерятся непредсказуемо off-tree. Снизили `RenderDpi` с 300 до 150 после того, как «Личные результаты» (длинный отчёт ~13K DIP) при 300 DPI давали 41K-px битмап и DirectX молча обрезал на ~16K px → пустые страницы.
