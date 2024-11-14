using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection.PortableExecutable;
using static VirtualMemSim.MainWindow;

namespace VirtualMemSim
{
    public partial class MainWindow : Window
    {
        // Параметры виртуальной памяти и таблицы страниц
        private int virtualMemorySize;
        private int ramSize;
        private int pageSize = 4; // Размер страницы в КБ по умолчанию
        private string replacementAlgorithm;

        // Коллекция для визуализации таблицы страниц
        public ObservableCollection<PageTableEntry> PageTable { get; set; }
        public ObservableCollection<MemoryBlock> MemoryBlocks { get; set; }
        // Счетчики для производительности
        private int memoryAccessCount;
        private int pageFaultCount;

        public class MemoryBlock
        {
            public int BlockId { get; set; }
            public string BlockColor { get; set; } // Цвет блока: например, "LightGreen" для заполненного блока и "LightGray" для пустого
        }


        public MainWindow()
        {
            InitializeComponent();
            PageTable = new ObservableCollection<PageTableEntry>();
            PageTableGrid.ItemsSource = PageTable;
            MemoryBlocks = new ObservableCollection<MemoryBlock>();
            memoryAccessCount = 0;
            pageFaultCount = 0;
            for (int i = 0; i < 50; i++)
            {
                MemoryBlocks.Add(new MemoryBlock
                {
                    BlockId = i + 1,
                    BlockColor = "LightGray" // Серый цвет для пустого блока
                });
            }

            // Устанавливаем DataContext для привязки
            DataContext = this;
        }

        private void UpdateMemoryBlock(int blockId, bool isOccupied)
        {
            var block = MemoryBlocks.FirstOrDefault(b => b.BlockId == blockId);
            if (block != null)
            {
                block.BlockColor = isOccupied ? "LightGreen" : "LightGray"; // Зеленый для занятого блока
            }
        }

        private void StartProcessButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация и задание параметров для запуска процесса
            if (int.TryParse(VirtualMemorySizeTextBox.Text, out virtualMemorySize) &&
                int.TryParse(RAMSizeTextBox.Text, out ramSize))
            {
                replacementAlgorithm = (ReplacementAlgorithmComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

                // Инициализация виртуального процесса
                InitializeVirtualProcess();

                // Запуск эмуляции
                StartVirtualMemorySimulation();
            }
            else
            {
                MessageBox.Show("Введите корректные значения для виртуальной и оперативной памяти.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopProcessButton_Click(object sender, RoutedEventArgs e)
        {
            StopVirtualMemorySimulation();
            MessageBox.Show("Процесс остановлен.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PageSizeTextBox.Text, out int newPageSize))
            {
                pageSize = newPageSize;
                MessageBox.Show("Параметры обновлены.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Введите корректное значение для размера страницы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowReportButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayPerformanceReport();
        }

        private void InitializeVirtualProcess()
        {
            // Очистка и инициализация таблицы страниц
            PageTable.Clear();
            memoryAccessCount = 0;
            pageFaultCount = 0;

            for (int i = 0; i < virtualMemorySize / pageSize; i++)
            {
                PageTable.Add(new PageTableEntry
                {
                    ProcessId = 1,
                    PageNumber = i,
                    FrameNumber = -1,
                    Status = "Не загружена"
                });
            }
        }

        private async void StartVirtualMemorySimulation()
        {
            switch (replacementAlgorithm)
            {
                case "FIFO":
                    await RunPageReplacementAsync(FIFOReplacement);
                    break;
                case "LRU":
                    await RunPageReplacementAsync(LRUReplacement);
                    break;
                case "Second Chance":
                    await RunPageReplacementAsync(SecondChanceReplacement);
                    break;
                default:
                    MessageBox.Show("Неизвестный алгоритм замещения. Выберите корректный алгоритм.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
            }
        }

        private async Task RunPageReplacementAsync(Func<int, Task> replacementAlgorithmFunc)
        {
            for (int i = 0; i < virtualMemorySize / pageSize; i++)
            {
                memoryAccessCount++; // Увеличиваем число обращений к памяти
                await replacementAlgorithmFunc(i);
                PageTableGrid.Items.Refresh();
                await Task.Delay(500);
            }
        }

        private async Task FIFOReplacement(int pageNumber)
        {
            if (PageTable[pageNumber].FrameNumber == -1)
            {
                pageFaultCount++; // Увеличиваем счетчик промахов страниц

                if (PageTable.Count(entry => entry.Status == "Загружена") >= ramSize / pageSize)
                {
                    // Замена страницы
                    var pageToReplace = PageTable.FirstOrDefault(entry => entry.Status == "Загружена");
                    if (pageToReplace != null)
                    {
                        pageToReplace.FrameNumber = -1; // Удаляем из памяти
                        pageToReplace.Status = "Не загружена";
                    }
                }

                // Загружаем новую страницу
                PageTable[pageNumber].FrameNumber = pageNumber;
                PageTable[pageNumber].Status = "Загружена";
            }
        }


        private async Task LRUReplacement(int pageNumber)
        {
            if (PageTable[pageNumber].FrameNumber == -1)
            {
                pageFaultCount++;
                if (PageTable.Count(entry => entry.Status == "Загружена") >= ramSize / pageSize)
                {
                    var leastRecentlyUsed = PageTable.OrderBy(entry => entry.PageNumber).FirstOrDefault(entry => entry.Status == "Загружена");
                    if (leastRecentlyUsed != null)
                    {
                        leastRecentlyUsed.FrameNumber = -1;
                        leastRecentlyUsed.Status = "Не загружена";
                    }
                }

                PageTable[pageNumber].FrameNumber = pageNumber;
                PageTable[pageNumber].Status = "Загружена";
            }
        }

        private async Task SecondChanceReplacement(int pageNumber)
        {
            if (PageTable[pageNumber].FrameNumber == -1)
            {
                pageFaultCount++;
                if (PageTable.Count(entry => entry.Status == "Загружена") >= ramSize / pageSize)
                {
                    foreach (var entry in PageTable)
                    {
                        if (entry.Status == "Загружена")
                        {
                            if (entry.FrameNumber % 2 == 0)
                            {
                                entry.FrameNumber = -1;
                                entry.Status = "Не загружена";
                                break;
                            }
                        }
                    }
                }

                PageTable[pageNumber].FrameNumber = pageNumber;
                PageTable[pageNumber].Status = "Загружена";
            }
        }

        private void StopVirtualMemorySimulation()
        {
            // Логика остановки симуляции
            // Например, можно сбросить счетчики или остановить любые активные задачи
            memoryAccessCount = 0;
            pageFaultCount = 0;
            PageTable.Clear(); // Очистить таблицу страниц, если необходимо
        }



        private void DisplayPerformanceReport()
        {
            MemoryAccessCount.Text = $"Число обращений к памяти: {memoryAccessCount}";
            PageFaultCount.Text = $"Число промахов страниц: {pageFaultCount}";

            // Рассчитываем процент успешных обращений
            double hitRate = memoryAccessCount > 0 ?
                ((memoryAccessCount - pageFaultCount) / (double)memoryAccessCount) * 100 : 0;
            HitRate.Text = $"Процент успешных обращений: {hitRate:F2}%";

            MessageBox.Show($"Отчет: Обращения к памяти: {memoryAccessCount}, Промахи страниц: {pageFaultCount}", "Отчет", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            // Пример обновления состояния блоков памяти: помечаем блоки с ID от 1 до 10 как занятые
            for (int i = 1; i <= 10; i++)
            {
                UpdateMemoryBlock(i, true);
            }

            // Можно добавить обратный вызов для освобождения блоков, например:
            for (int i = 11; i <= 20; i++)
            {
                UpdateMemoryBlock(i, false);
            }
        }

    }

    public class PageTableEntry
    {
        public int ProcessId { get; set; }
        public int PageNumber { get; set; }
        public int FrameNumber { get; set; }
        public string Status { get; set; }
    }
}
