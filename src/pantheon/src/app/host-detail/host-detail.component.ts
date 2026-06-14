import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridApi, GridReadyEvent, RowSelectedEvent } from 'ag-grid-community';
import { KnobModule } from 'primeng/knob';
import { ProgressBarModule } from 'primeng/progressbar';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent, DataZoomComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { ArgusApiService } from '../core/argus-api.service';
import { LiveService } from '../core/live.service';
import { ThemeService } from '../core/theme.service';
import { BytesPipe } from '../core/bytes.pipe';
import { DiskPoint, Host, LiveMetric, MetricPoint, MetricsResponse, ProcessHistoryPoint, ProcessInfo } from '../core/models';

echarts.use([LineChart, GridComponent, TooltipComponent, LegendComponent, DataZoomComponent, CanvasRenderer]);

@Component({
  selector: 'app-host-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    AgGridAngular, KnobModule, ProgressBarModule, CardModule, ButtonModule, SelectButtonModule,
    NgxEchartsDirective,
    BytesPipe,
  ],
  providers: [provideEchartsCore({ echarts })],
  templateUrl: './host-detail.component.html'
})
export class HostDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  readonly theme = inject(ThemeService);
  private subs: Subscription[] = [];
  private gridApi?: GridApi;

  readonly Math = Math;

  hostId = 0;
  host = signal<Host | undefined>(undefined);
  metric = signal<LiveMetric | undefined>(undefined);
  processes = signal<ProcessInfo[]>([]);
  snapshotAt = signal<string | null>(null);

  // history
  historyWindow = signal<'1h' | '6h' | '24h'>('1h');
  metricsHistory = signal<MetricPoint[]>([]);
  disksHistory = signal<DiskPoint[]>([]);
  selectedProcess = signal<ProcessInfo | null>(null);
  processHistory = signal<ProcessHistoryPoint[]>([]);
  processHistoryLoading = signal(false);

  windowOptions = [
    { label: '1 h',  value: '1h'  },
    { label: '6 h',  value: '6h'  },
    { label: '24 h', value: '24h' },
  ];

  defaultColDef: ColDef = { sortable: true, resizable: true, filter: true, flex: 1 };

  processCols: ColDef<ProcessInfo>[] = [
    { field: 'pid', headerName: 'PID', flex: 0, width: 80 },
    { field: 'name', headerName: 'Name', flex: 2 },
    { field: 'cpuPercent', headerName: 'CPU %', valueFormatter: p => `${Math.round(p.value ?? 0)}%`, sort: 'desc' },
    { field: 'memoryBytes', headerName: 'Memory', valueFormatter: p => formatBytes(p.value) },
    { field: 'threadCount', headerName: 'Threads', flex: 0, width: 100 },
  ];

  ngOnInit(): void {
    this.hostId = Number(this.route.snapshot.paramMap.get('id'));

    this.api.getHost(this.hostId).subscribe(h => this.host.set(h));

    this.api.getProcesses(this.hostId).subscribe(s => {
      this.processes.set(s.processes);
      this.snapshotAt.set(s.timestampUtc);
    });

    this.loadMetricsHistory();

    this.subs.push(
      this.live.metric.subscribe(m => { if (m.hostId === this.hostId) this.metric.set(m); }),
      this.live.processes.subscribe(p => {
        if (p.hostId === this.hostId) {
          this.processes.set(p.processes);
          this.snapshotAt.set(p.timestampUtc);
        }
      })
    );

    this.live.subscribe(this.hostId);
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
    this.live.unsubscribe(this.hostId);
  }

  onGridReady(e: GridReadyEvent): void {
    this.gridApi = e.api;
  }

  onRowSelected(e: RowSelectedEvent<ProcessInfo>): void {
    if (!e.node.isSelected()) return;
    const proc = e.data;
    if (!proc) return;
    this.selectedProcess.set(proc);
    this.loadProcessHistory(proc.name);
  }

  clearProcessSelection(): void {
    this.selectedProcess.set(null);
    this.processHistory.set([]);
    this.gridApi?.deselectAll();
  }

  changeWindow(w: '1h' | '6h' | '24h'): void {
    this.historyWindow.set(w);
    this.loadMetricsHistory();
    const sel = this.selectedProcess();
    if (sel) this.loadProcessHistory(sel.name);
  }

  memPercent(m?: LiveMetric): number {
    if (!m || !m.memoryTotalBytes) return 0;
    return Math.round((m.memoryUsedBytes / m.memoryTotalBytes) * 100);
  }

  diskPercent(total: number, used: number): number {
    return total ? Math.round((used / total) * 100) : 0;
  }

  // ---- ECharts option builders ----

  systemChartOption(history: MetricPoint[]): any {
    const dark = this.theme.isDark();
    const times = history.map(p => new Date(p.timestampUtc).toLocaleTimeString());
    return {
      backgroundColor: 'transparent',
      textStyle: { color: dark ? '#ccc' : '#333' },
      tooltip: { trigger: 'axis' },
      legend: { data: ['CPU %', 'RAM %'], textStyle: { color: dark ? '#ccc' : '#333' } },
      dataZoom: [{ type: 'inside' }],
      xAxis: { type: 'category', data: times, axisLabel: { color: dark ? '#aaa' : '#555' } },
      yAxis: { type: 'value', min: 0, max: 100, axisLabel: { formatter: '{value}%', color: dark ? '#aaa' : '#555' } },
      series: [
        {
          name: 'CPU %',
          type: 'line',
          smooth: true,
          showSymbol: false,
          data: history.map(p => Math.round(p.cpuPercent)),
          areaStyle: { opacity: 0.15 },
          lineStyle: { width: 2 },
          color: '#3b82f6',
        },
        {
          name: 'RAM %',
          type: 'line',
          smooth: true,
          showSymbol: false,
          data: history.map(p => p.memoryTotalBytes ? Math.round(p.memoryUsedBytes / p.memoryTotalBytes * 100) : 0),
          areaStyle: { opacity: 0.15 },
          lineStyle: { width: 2 },
          color: '#10b981',
        },
      ],
      grid: { left: 50, right: 20, top: 40, bottom: 40 },
    };
  }

  diskChartOption(disks: DiskPoint[]): any {
    const dark = this.theme.isDark();
    const mounts = [...new Set(disks.map(d => d.mount))];
    const colors = ['#f59e0b', '#ec4899', '#8b5cf6', '#06b6d4'];
    return {
      backgroundColor: 'transparent',
      textStyle: { color: dark ? '#ccc' : '#333' },
      tooltip: { trigger: 'axis', valueFormatter: (v: any) => `${v}%` },
      legend: { data: mounts, textStyle: { color: dark ? '#ccc' : '#333' } },
      dataZoom: [{ type: 'inside' }],
      xAxis: { type: 'category', boundaryGap: false, axisLabel: { color: dark ? '#aaa' : '#555' } },
      yAxis: { type: 'value', min: 0, max: 100, axisLabel: { formatter: '{value}%', color: dark ? '#aaa' : '#555' } },
      series: mounts.map((mount, i) => {
        const pts = disks.filter(d => d.mount === mount);
        return {
          name: mount,
          type: 'line',
          smooth: true,
          showSymbol: false,
          color: colors[i % colors.length],
          data: pts.map(d => [new Date(d.timestampUtc).toLocaleTimeString(), Math.round(d.usedBytes / d.totalBytes * 100)]),
          lineStyle: { width: 2 },
        };
      }),
      grid: { left: 50, right: 20, top: 40, bottom: 40 },
    };
  }

  processChartOption(history: ProcessHistoryPoint[], name: string): any {
    const dark = this.theme.isDark();
    const times = history.map(p => new Date(p.timestampUtc).toLocaleTimeString());
    return {
      backgroundColor: 'transparent',
      textStyle: { color: dark ? '#ccc' : '#333' },
      tooltip: { trigger: 'axis' },
      legend: { data: ['CPU %', 'Threads'], textStyle: { color: dark ? '#ccc' : '#333' } },
      dataZoom: [{ type: 'inside' }],
      xAxis: { type: 'category', data: times, axisLabel: { color: dark ? '#aaa' : '#555' } },
      yAxis: [
        { type: 'value', name: 'CPU %', axisLabel: { formatter: '{value}%', color: dark ? '#aaa' : '#555' } },
        { type: 'value', name: 'Threads', axisLabel: { color: dark ? '#aaa' : '#555' } },
      ],
      series: [
        {
          name: 'CPU %',
          type: 'line',
          smooth: true,
          showSymbol: false,
          yAxisIndex: 0,
          data: history.map(p => Math.round(p.cpuPercent)),
          areaStyle: { opacity: 0.15 },
          lineStyle: { width: 2 },
          color: '#3b82f6',
        },
        {
          name: 'Threads',
          type: 'line',
          smooth: true,
          showSymbol: false,
          yAxisIndex: 1,
          data: history.map(p => p.threadCount),
          lineStyle: { width: 2, type: 'dashed' },
          color: '#f59e0b',
        },
      ],
      grid: { left: 60, right: 60, top: 40, bottom: 40 },
    };
  }

  processMemChartOption(history: ProcessHistoryPoint[]): any {
    const dark = this.theme.isDark();
    const times = history.map(p => new Date(p.timestampUtc).toLocaleTimeString());
    return {
      backgroundColor: 'transparent',
      textStyle: { color: dark ? '#ccc' : '#333' },
      tooltip: {
        trigger: 'axis',
        valueFormatter: (v: any) => formatBytes(v as number),
      },
      dataZoom: [{ type: 'inside' }],
      xAxis: { type: 'category', data: times, axisLabel: { color: dark ? '#aaa' : '#555' } },
      yAxis: {
        type: 'value',
        axisLabel: {
          formatter: (v: number) => formatBytes(v),
          color: dark ? '#aaa' : '#555',
        },
      },
      series: [{
        name: 'Memory',
        type: 'line',
        smooth: true,
        showSymbol: false,
        data: history.map(p => p.memoryBytes),
        areaStyle: { opacity: 0.15 },
        lineStyle: { width: 2 },
        color: '#10b981',
      }],
      grid: { left: 80, right: 20, top: 30, bottom: 40 },
    };
  }

  private loadMetricsHistory(): void {
    const { from, to } = windowRange(this.historyWindow());
    this.api.getMetrics(this.hostId, from, to).subscribe((r: MetricsResponse) => {
      this.metricsHistory.set(r.metrics);
      this.disksHistory.set(r.disks);
    });
  }

  private loadProcessHistory(name: string): void {
    this.processHistoryLoading.set(true);
    const { from, to } = windowRange(this.historyWindow());
    this.api.getProcessHistory(this.hostId, name, from, to).subscribe(pts => {
      this.processHistory.set(pts);
      this.processHistoryLoading.set(false);
    });
  }
}

function windowRange(w: '1h' | '6h' | '24h'): { from: Date; to: Date } {
  const to = new Date();
  const hours = w === '1h' ? 1 : w === '6h' ? 6 : 24;
  const from = new Date(to.getTime() - hours * 3_600_000);
  return { from, to };
}

function formatBytes(bytes: number): string {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}
