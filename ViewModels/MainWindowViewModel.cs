using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;

using ManagedNativeWifi;

using Microsoft.Win32;

using SkiaSharp;

using TensometerPoltter.Infrastructure.Commands;
using TensometerPoltter.Models;
using TensometerPoltter.ViewModels.Base;

namespace TensometerPoltter.ViewModels
{
    internal class MainWindowViewModel : BaseViewModel
    {

        #region ОТЛАДКА
        // Для генерации случайных чисел вместо данных
        //private readonly Random random;
        #endregion ОТЛАДКА

        #region ПЕРЕМЕННЫЕ
        // Объект для работы с UDP
        private readonly UDPModel udp;
        // Флаг "чтения" данных
        private bool isReading;
        // Разрядность медианного усреднения
        private readonly int avgValueDepth;
        // Разрядность медианного усреднения
        private readonly int medianDepth;
        // Буфер данных
        private readonly int[] medianBuffer;
        // Счетчик буфера
        private int medianCounter;
        // Список для записи значений
        private readonly List<int> valuesForSave;
        // Флаг переключения записи данных
        private bool dataWriteFlag;
        // Максимальное число значений на графике
        private readonly int maxChartValues;
        #endregion ПЕРЕМЕННЫЕ

        #region СВОЙСТВА
        // Отображение статуса исполнения
        private string status;
        public string Status
        {
            get => status;
            set => Set(ref status, value);
        }

        // Отображение статуса работы с файлом
        private string statusDataFile;
        public string StatusDataFile
        {
            get => statusDataFile;
            set => Set(ref statusDataFile, value);
        }

        // Объект для синхронизации данных между потоками
        public object Sync { get; }

        // Флаг скользящего среднего для оборотов
        private bool movingTurnAverage;
        public bool MovingTurnAverage
        {
            get => movingTurnAverage;
            set => Set(ref movingTurnAverage, value);
        }

        // Флаг скользящего среднего для значений АЦП
        private bool movingValueAverage;
        public bool MovingValueAverage
        {
            get => movingValueAverage;
            set => Set(ref movingValueAverage, value);
        }

        // Флаг медианного усреднения
        private bool medianValueAverage;
        public bool MedianValueAverage
        {
            get => medianValueAverage;
            set => Set(ref medianValueAverage, value);
        }

        // Максимальное значение датчика
        private int maxTenzValue;
        public int MaxTenzValue
        {
            get => maxTenzValue;
            set => Set(ref maxTenzValue, value);
        }

        // Минимальное значение датчика
        private int minTenzValue;
        public int MinTenzValue
        {
            get => minTenzValue;
            set => Set(ref minTenzValue, value);
        }

        // Для отображения приходящих оборотов
        private int incomingTurnData;
        public int IncomingTurnData
        {
            get => incomingTurnData;
            set => Set(ref incomingTurnData, value);
        }

        // Список масштабов для графика
        private List<string> scalesChart;
        public List<string> ScalesChart
        {
            get => scalesChart;
            set => Set(ref scalesChart, value);
        }

        // Текущий масштаб граифка
        private int selectedScaleIndexChart;
        public int SelectedScaleIndexChart
        {
            get => selectedScaleIndexChart;
            set
            {
                Set(ref selectedScaleIndexChart, value);
                ResetScaleMethod();
            }
        }

        // Текст для кнопки 
        #region ДЛЯ ГРАФИКОВ
        // Масштаб оси X
        private Axis scaleX;
        public Axis ScaleX
        {
            get => scaleX;
            set => Set(ref scaleX, value);
        }
        // Масштаб оси Y
        private Axis[] scaleY;
        public Axis[] ScaleY
        {
            get => scaleY;
            set => Set(ref scaleY, value);
        }

        // Данные графика
        public ObservableCollection<ObservableCollection<int>> ValuesForSeriesPlot { get; set; }
        // Для удаленеия полос оборотов
        private readonly List<int> turnControlerLines;

        // Для отображения графика (Серии)
        public ISeries[] SeriesPlot { get; set; }
        #endregion ДЛЯ ГРАФИКОВ
        #endregion СВОЙСТВА

        #region КОМАНДЫ 
        #region StartCmd
        public ICommand StartCmd { get; }
        public void OnStartCmdExecuted(object param)
        {
            try
            {
                OnResetDataCmdExecuted(param);
                isReading = true;
                SendData();
                Thread.Sleep(250);
                _ = PaintData();
                Status = "Чтение";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private bool CanStartCmdExecute(object param)
        {
            bool checkWifi = false;
            // Если есть беспроводная сеть
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.NetworkInterfaceType.ToString().Contains("Wireless"))
                    checkWifi = true;
            }
            // Если такая сеть есть
            if (checkWifi)
            {
                // То ищем нужный Wi-Fi - иначе неверно
                foreach (var connectionName in NativeWifi.EnumerateInterfaceConnections())
                {
                    if (connectionName.ProfileName == "Tenz")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion StartCmd
        #region EndCmd
        public ICommand EndCmd { get; }
        public void OnEndCmdExecuted(object param)
        {
            Status = "Ожидание";
            isReading = false;
        }
        private bool CanEndCmdExecute(object param) => true;
        #endregion EndCmd
        #region ResetDataCmd
        public ICommand ResetDataCmd { get; }
        public void OnResetDataCmdExecuted(object param)
        {
            lock (Sync)
            {
                foreach (var item in ValuesForSeriesPlot)
                    item.Clear();
            }
            OnEndCmdExecuted(param);
        }
        private bool CanResetDataCmdExecute(object param) => true;
        #endregion ResetDataCmd
        #region ResetScaleCmd

        void ResetScaleMethod()
        {
            if (selectedScaleIndexChart != 0)
            {
                var range = 1 + 4 * selectedScaleIndexChart;
                var min = -1 * Convert.ToInt32(scalesChart[selectedScaleIndexChart]);
                var max = Convert.ToInt32(scalesChart[selectedScaleIndexChart]);
                if (selectedScaleIndexChart <= 3)
                {
                    ScaleY[0].CustomSeparators = Enumerable.Range(0, range).Select(i => (double)(min + 50 * i)).ToArray();
                    ScaleY[0].MinLimit = min;
                    ScaleY[0].MaxLimit = max;
                }
                else
                {
                    ScaleY[0].CustomSeparators = Enumerable.Range(0, range).Select(i => (double)(min + 100 * i)).ToArray();
                    ScaleY[0].MinLimit = min;
                    ScaleY[0].MaxLimit = max;
                }
            }
            else
            {
                ScaleY[0].CustomSeparators = null;
                ScaleY[0].MinLimit = null;
                ScaleY[0].MaxLimit = null;
            }


            ScaleX.MinLimit = null;
            ScaleX.MaxLimit = null;
            OnPropertyChanged(nameof(ScaleX));
            OnPropertyChanged(nameof(ScaleY));
        }

        public ICommand ResetScaleCmd { get; }
        public void OnResetScaleCmdExecuted(object param)
        {
            ResetScaleMethod();
        }
        private bool CanResetScaleCmdExecute(object param) => true;
        #endregion ResetCmd
        #region SwitchRecordDataCmd
        public ICommand SwitchRecordDataCmd { get; }
        public void OnSwitchRecordDataCmdExecuted(object param)
        {
            // Отключить запись, если она включена
            if (dataWriteFlag)
            {
                dataWriteFlag = false;
                statusDataFile = "Данные не записываются";
            }
            // Включить запись, если она отключена
            else
            {
                dataWriteFlag = true;
                statusDataFile = "Данные записываются";
            }
        }
        private bool CanSwitchRecordDataCmdExecute(object param) => isReading;
        #endregion SwitchRecordDataCmd
        #region SaveFileCmd
        public ICommand SaveFileCmd { get; }
        public void OnSaveFileCmdExecuted(object param)
        {
            // Перестаем читать данные
            isReading = false;
            // Смена статуса
            Status = "Ожидание";
            // Снятие флага записи в данныех
            dataWriteFlag = false;
            // Смена статуса данных
            statusDataFile = "Данные не записываются";

            // Сохранение коллекции в файл
            SaveFileDialog saveFileDialog = new()
            {
                FileName = "TenzometerResult",
                DefaultExt = ".ini",
                Filter = "Ini files(*.ini)|*.ini|ADC files(*.adc)|*.adc"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // Запоминаем имя файла
                var fileName = saveFileDialog.FileName;
                // Открываем поток записи файла
                using FileStream writer = new(fileName + ".ini", FileMode.Create);
                using BinaryWriter binWriter = new(writer, Encoding.UTF8);

                // Заполняем данными
                string header = "[Analog-Digital Conversion]\r\n"
                + "Title = СТАРТОВЫЙ ТЕСТ.Кадры:00 - 16.Дата:03.08.19.Время: 10:41:20\r\n"
                + "Interval = 500\r\n"
                + "AmpChannel = 1\r\n"
                + "RevChannel = 2\r\n"
                + "RevLevel = 512\r\n"
                + "RevLevelHor = 0\r\n"
                + "Reduction = 1.00\r\n\r\n"
                + "[Channel 01]\r\n"
                + "Intense = 1\r\n"
                + "TarInt = 5\r\n"
                + "TarCode = 2048\r\n"
                + "TarUnit = 1024\r\n"
                + "TarName = мв\r\n\r\n"
                + "[Channel 02]\r\n"
                + "Intense = 1\r\n"
                + "TarInt = 1\r\n"
                + "TarCode =\r\n"
                + "TarUnit =\r\n"
                + "TarName = об / мин\r\n";

                binWriter.Write(Encoding.UTF8.GetBytes(header));

                // Открываем поток записи файла
                using FileStream writerData = new(fileName + ".ini", FileMode.Create);
                using BinaryWriter binWriterData = new(writerData, Encoding.UTF8);

                // Заполняем данными (Сохраняем каждое 10ое значение (Усреднение))
                for (int i = 0; i < valuesForSave.Count / 10; i++)
                {
                    int sing = 0;
                    int value = 0;
                    for (int ii = 0; ii < 10; ii++)
                    {
                        sing += valuesForSave[i * 10 + ii] & 0x8000;
                        value += valuesForSave[i * 10 + ii];
                    }
                    value /= 10;
                    binWriterData.Write((short)value);
                    binWriterData.Write((short)sing);
                }
            }

        }
        private bool CanSaveFileCmdExecute(object param) => (valuesForSave.Count > 0);
        #endregion SaveFileCmd

        #endregion КОМАНДЫ

        public MainWindowViewModel()
        {
            udp = new(4220, 4210);
            avgValueDepth = 4;
            medianDepth = 5;
            medianBuffer = new int[medianDepth];
            medianCounter = 0;

            Sync = new object();
            incomingTurnData = 0;
            scalesChart = new() {
                "auto", "100", "200", "300", "400", "500", "600"
            };
            status = "Ожидание";
            statusDataFile = "Данные не записываются";
            movingTurnAverage = false;
            movingValueAverage = false;
            medianValueAverage = false;
            maxTenzValue = 0;
            minTenzValue = 0;
            valuesForSave = new();
            dataWriteFlag = false;
            maxChartValues = 250;

            scaleX = new();
            scaleY = new[]
            {
                new Axis()
            };

            ValuesForSeriesPlot = new()
            {
                new ObservableCollection<int>
                {
                    1, 2, 5, 6, 1, 6, 3, 8, 9, 10
                },
                new ObservableCollection<int>
                {
                    10, 9, 8, 3, 6, 1, 6, 5, 2, 1
                }
            };

            turnControlerLines = new();

            SeriesPlot = new ISeries[]
            {
                new LineSeries<int>
                {
                    Values = ValuesForSeriesPlot[0],
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.CornflowerBlue, 3),
                },
                new ColumnSeries<int>
                {
                    Values = ValuesForSeriesPlot[1],
                    MaxBarWidth = 5,
                    Fill = new SolidColorPaint(SKColors.Red)
                }
            };

            StartCmd = new LambdaCommand(OnStartCmdExecuted, CanStartCmdExecute);
            EndCmd = new LambdaCommand(OnEndCmdExecuted, CanEndCmdExecute);
            ResetDataCmd = new LambdaCommand(OnResetDataCmdExecuted, CanResetDataCmdExecute);
            ResetScaleCmd = new LambdaCommand(OnResetScaleCmdExecuted, CanResetScaleCmdExecute);
            SwitchRecordDataCmd = new LambdaCommand(OnSwitchRecordDataCmdExecuted, CanSwitchRecordDataCmdExecute);
            SaveFileCmd = new LambdaCommand(OnSaveFileCmdExecuted, CanSaveFileCmdExecute);
        }

        private async void SendData()
        {
            await udp.SendMessageAsync("RGUPS", "192.168.4.23", 4210);
            Status = "Данные отправлены: Data: RGUPS, IP: 192.168.4.23, Port: 4210";
        }

        private int MedianAvg(int value)
        {
            // Добавление элемента в буфер
            medianBuffer[medianCounter] = value;
            // Подъем элемента
            if ((medianCounter < medianDepth - 1) && (medianBuffer[medianCounter] > medianBuffer[medianCounter + 1]))
            {
                for (int i = medianCounter; i < medianDepth - 1; i++)
                {
                    if (medianBuffer[i] > medianBuffer[i + 1])
                        (medianBuffer[i], medianBuffer[i + 1]) = (medianBuffer[i + 1], medianBuffer[i]);
                }
            }
            // Опускание элемента
            else if ((medianCounter > 0) && (medianBuffer[medianCounter - 1] > medianBuffer[medianCounter]))
            {
                for (int i = medianCounter; i > 0; i--)
                {
                    if (medianBuffer[i] < medianBuffer[i - 1])
                        (medianBuffer[i], medianBuffer[i - 1]) = (medianBuffer[i - 1], medianBuffer[i]);
                }
            }
            if (++medianCounter >= medianDepth)
                medianCounter = 0;
            return medianBuffer[medianDepth / 2];
        }

        private async Task PaintData()
        {
            try
            {
                await Task.Run(() =>
                {
                    //Очистка буфера перед приемом данных
                    udp.ClearBuffer();

                    // Предыдущее значение оборотов
                    int oldTurnovers = 0;

                    while (isReading)
                    {
                        lock (Sync)
                        {
                            // Получение списка byte
                            var result = udp.ReceiveMessageAsync().Result;

                            if (result.Count > 0)
                            {
                                // Длина всего массива данных
                                var fullLength = result.Count;
                                // Длина массива данных тензометра
                                var tenzLength = result.Count - 6;

                                for (int i = 0; i < tenzLength; i++)
                                {
                                    // Преобразование данных к int
                                    int temp = result[i++] << 8 | result[i];
                                    // Убираем флаг оборота
                                    int tempValue = (temp & 0x7FFF) - 512;
                                    // Фильтрация медианным усреднением
                                    if (medianValueAverage)
                                    {
                                        tempValue = MedianAvg(tempValue);
                                    }
                                    // Фильтрация скользящим средним
                                    if (movingValueAverage)
                                    {
                                        var chartLength = ValuesForSeriesPlot[0].Count;
                                        if (chartLength > avgValueDepth)
                                        {
                                            for (int ii = 1; ii < avgValueDepth; ii++)
                                            {
                                                tempValue += ValuesForSeriesPlot[0].ElementAt(chartLength - ii);
                                            }
                                            tempValue /= avgValueDepth;
                                        }
                                    }
                                    // Добавление данных на график
                                    ValuesForSeriesPlot[0].Add(tempValue);
                                    // Если в этом пакете есть "метка" оборота
                                    if ((temp & 0x8000) == 1)
                                    {
                                        ValuesForSeriesPlot[1].Add(512);
                                        turnControlerLines.Add(maxChartValues);
                                    }

                                    // Максимально отображается 250 элементов
                                    if (ValuesForSeriesPlot[0].Count > maxChartValues)
                                        ValuesForSeriesPlot[0].RemoveAt(0);

                                    // Если линия, показывающая оборот прошла 250 значений, удаляем ее из коллекции
                                    for (int ii = 0; ii < turnControlerLines.Count; ii++)
                                    {
                                        if (turnControlerLines[ii] == 0)
                                            ValuesForSeriesPlot[1].RemoveAt(0);
                                        else
                                            turnControlerLines[ii]--;
                                    }

                                    // Очищаем из коллекции, которая следила за "жизнью" линии оборота ненужные значения
                                    if (turnControlerLines.Count != ValuesForSeriesPlot[1].Count)
                                    {
                                        int different = Math.Abs(turnControlerLines.Count - ValuesForSeriesPlot[1].Count);
                                        for (int ii = 0; ii < different; ii++)
                                        {
                                            turnControlerLines.RemoveAt(0);
                                        }
                                    }
                                }

                                // Запись данных в коллекцию для дальнейшего сохранения их в файл
                                if (dataWriteFlag)
                                    valuesForSave.Add(ValuesForSeriesPlot[0][^1]);

                                // Временные значения максимума и минимума для их поиска
                                var minTemp = ValuesForSeriesPlot[0][0];
                                var maxTemp = ValuesForSeriesPlot[0][0];

                                // Поиск минимального и максимального
                                foreach (var value in ValuesForSeriesPlot[0])
                                {
                                    if (minTemp > value)
                                        minTemp = value;
                                    if (maxTemp < value)
                                        maxTemp = value;
                                }
                                MaxTenzValue = maxTemp;
                                MinTenzValue = minTemp;

                                #region Расчет оборотов
                                // Получение значения оборотов
                                var tempTurnovers = (int)(result[fullLength - 3] << 8 | result[fullLength - 2]);
                                if (movingTurnAverage)
                                    oldTurnovers = (int)((oldTurnovers * 19 + tempTurnovers) / 20);
                                else
                                    oldTurnovers = tempTurnovers;
                                #endregion Расчет оборотов

                                IncomingTurnData = oldTurnovers;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
