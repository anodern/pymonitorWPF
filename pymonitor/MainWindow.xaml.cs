using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace pymonitor {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow:Window {
        public MainWindow() {
            InitializeComponent();
        }

        //性能计数器
        private PerformanceCounter readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec");
        private PerformanceCounter writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec");

        //折线点
        private ObservableDataSource<Point> dataSource_cpu = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_ram = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_read = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> dataSource_write = new ObservableDataSource<Point>();

        //定时器
        private DispatcherTimer timerIdle = new DispatcherTimer();
        private DispatcherTimer timer = new DispatcherTimer();
        private int interval = 100;
        private TimeSpan prevCpuTime;

        //图表
        private int currentSecond = 0;
        private int xCount = 60;//x轴数量
        private int xAxis = 0;
        private double cpuMax = 0;
        private double ramMax = 0;
        private double readMax = 0;
        private double writeMax = 0;

        //点队列 计算y轴最大值
        private Queue q_cpu = new Queue();
        private Queue q_ram = new Queue();
        private Queue q_read = new Queue();
        private Queue q_write = new Queue();

        //python进程
        private Process[] p;
        private int index;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            //折线
            plotter_cpu.AddLineGraph(dataSource_cpu, Colors.Red, 1.5);
            plotter_ram.AddLineGraph(dataSource_ram, Colors.Red, 1.5);
            plotter_read.AddLineGraph(dataSource_read, Colors.Red, 1.5);
            plotter_write.AddLineGraph(dataSource_write, Colors.Red, 1.5);

            //关闭图例
            plotter_cpu.LegendVisible = false;
            plotter_ram.LegendVisible = false;
            plotter_read.LegendVisible = false;
            plotter_write.LegendVisible = false;

            plotter_cpu.Viewport.FitToView();
            plotter_ram.Viewport.FitToView();
            plotter_read.Viewport.FitToView();
            plotter_write.Viewport.FitToView();

            //图表计时器
            timer.Interval = TimeSpan.FromMilliseconds(interval);
            timer.Tick += timer_Tick;
            timer.IsEnabled = false;

            //刷新进程计时器
            timerIdle.Interval = TimeSpan.FromSeconds(2);
            timerIdle.Tick += timerIdle_Tick;
            timerIdle.IsEnabled = false;

            RefreshProcessList();
        }

        private void btn_refresh_Click(object sender, RoutedEventArgs e) {
            RefreshProcessList();
        }

        private void timer_Tick(object sender, EventArgs e) {
            if(index >= p.Length || p[index].HasExited) {
                StopTimer();
                return;
            }

            //cpu和内存
            TimeSpan curTime = p[index].TotalProcessorTime;
            double cpu = (curTime - prevCpuTime).TotalMilliseconds / interval /Environment.ProcessorCount * 100;
            prevCpuTime = curTime;
            double ram = p[index].WorkingSet64/1048576.0;

            //磁盘读写
            double readByte, writeByte;
            try {
                //ram = ramCounter.NextValue()/1048576.0;
                readByte = readBytesSec.NextValue()/1024.0;
                writeByte = writeByteSec.NextValue()/1024.0;
            }catch(InvalidOperationException) {
                StopTimer();
                return;
            }


            //折线图添加点
            Point point_cpu = new Point(currentSecond, cpu);
            Point point_ram = new Point(currentSecond, ram);
            Point point_read = new Point(currentSecond, readByte);
            Point point_write = new Point(currentSecond, writeByte);
            dataSource_cpu.AppendAsync(Dispatcher, point_cpu);
            dataSource_ram.AppendAsync(Dispatcher, point_ram);
            dataSource_read.AppendAsync(Dispatcher, point_read);
            dataSource_write.AppendAsync(Dispatcher, point_write);


            if(currentSecond - xCount > 0) xAxis = currentSecond - xCount;
            else xAxis = 0;

            //计算最大值
            //cpu
            if(q_ram.Count >= xCount) q_ram.Dequeue();
            q_ram.Enqueue(cpu);
            cpuMax = 0;
            foreach(double c in q_ram) {
                if(c > cpuMax) cpuMax = c;
            }

            //内存
            if(q_ram.Count >= xCount) q_ram.Dequeue();
            q_ram.Enqueue(ram);
            ramMax = 0;
            foreach(double c in q_ram) {
                if(c > ramMax) ramMax = c;
            }

            //read
            if(q_read.Count >= xCount) q_read.Dequeue();
            q_read.Enqueue(readByte);
            readMax = 0;
            foreach(double c in q_read) {
                if(c > readMax) readMax = c;
            }

            //write
            if(q_write.Count >= xCount) q_write.Dequeue();
            q_write.Enqueue(writeByte);
            writeMax = 0;
            foreach(double c in q_write) {
                if(c > writeMax) writeMax = c;
            }



            //label显示数据
            label_cpu.Content = string.Format("{0:F2} %", cpu);
            label_memory.Content = string.Format("{0:F2} MB", ram);
            label_read.Content = string.Format("{0:F2} KB/s", readByte);
            label_write.Content = string.Format("{0:F2} KB/s", writeByte);

            label_cpuMax.Content = string.Format("{0:F2} %", cpuMax);
            label_memoryMax.Content = string.Format("{0:F2} MB", ramMax);
            label_readMax.Content = string.Format("{0:F2} KB/s", readMax);
            label_writeMax.Content = string.Format("{0:F2} KB/s", writeMax);

            //设定显示区域
            plotter_cpu.Viewport.Visible = new Rect(xAxis, 0, xCount, 100);
            plotter_ram.Viewport.Visible = new Rect(xAxis, 0, xCount, ramMax*1.1);
            plotter_read.Viewport.Visible = new Rect(xAxis, 0, xCount, readMax*1.1);
            plotter_write.Viewport.Visible = new Rect(xAxis, 0, xCount, writeMax*1.1);
            currentSecond++;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            timer.Stop();
            StartTimer();
        }
        private void btn_start_Click(object sender, RoutedEventArgs e) {
            StartTimer();
        }
        private void btn_stop_Click(object sender, RoutedEventArgs e) {
            timer.Stop();
            if(combo_process.SelectedIndex>=0) {
                btn_start.IsEnabled = true;
            }
        }

        private void StartTimer() {
            index = combo_process.SelectedIndex;
            if(index<0 || p[index].HasExited) {
                RefreshProcessList();
                return;
            }

            string pname = "python";
            if(index>0) {
                pname += "#"+index;
            }

            readBytesSec.InstanceName = pname;
            writeByteSec.InstanceName = pname;

            prevCpuTime = p[index].TotalProcessorTime;
            timer.Start();
            btn_start.IsEnabled = false;
            btn_stop.IsEnabled = true;
        }

        private void StopTimer() {
            timer.Stop();
            RefreshProcessList();
        }

        private void RefreshProcessList() {
            //刷新进程列表
            combo_process.Items.Clear();
            p = Process.GetProcessesByName("python");
            for(int i = 0; i<p.Length; i++) {
                combo_process.Items.Add(p[i].ProcessName + (i>0 ? "#"+i : ""));
            }
            btn_start.IsEnabled = false;
            btn_stop.IsEnabled = false;
            label_count.Content = p.Length;

            //未启用python,每2s刷新一次
            if(p.Length == 0) {
                if(!timerIdle.IsEnabled) {
                    timerIdle.Start();
                }
            } else {
                timerIdle.Stop();
            }
        }

        private void timerIdle_Tick(object sender, EventArgs e) {
            RefreshProcessList();
        }
    }
}
