import { Component, OnDestroy, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { CardModule } from 'primeng/card';
import { ProgressBarModule } from 'primeng/progressbar';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef } from 'ag-grid-community';
import { ArgusApiService } from '../core/argus-api.service';
import { LiveService } from '../core/live.service';
import { BytesPipe } from '../core/bytes.pipe';
import { Host, LiveMetric } from '../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    CardModule, ProgressBarModule, TagModule,
    InputTextModule, ButtonModule, TooltipModule,
    AgGridAngular,
    BytesPipe,
  ],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit, OnDestroy {
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  private subs: Subscription[] = [];
  private clockInterval?: ReturnType<typeof setInterval>;

  hosts = signal<Host[]>([]);
  metrics = signal<Record<number, LiveMetric>>({});
  processCounts = signal<Record<number, number>>({});
  now = signal<number>(Date.now());

  search = signal('');
  viewMode = signal<'card' | 'grid'>('card');

  filteredHosts = computed(() => {
    const q = this.search().toLowerCase().trim();
    return q ? this.hosts().filter(h => h.machineName.toLowerCase().includes(q)) : this.hosts();
  });

  ngOnInit(): void {
    this.api.getHosts().subscribe(async hosts => {
      this.hosts.set(hosts);
      this.subs.push(
        this.live.metric.subscribe(m =>
          this.metrics.update(cur => ({ ...cur, [m.hostId]: m }))),
        this.live.processes.subscribe(p =>
          this.processCounts.update(cur => ({ ...cur, [p.hostId]: p.processes.length })))
      );
      for (const h of hosts) await this.live.subscribe(h.id);
    });
    this.clockInterval = setInterval(() => this.now.set(Date.now()), 15_000);
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
    clearInterval(this.clockInterval);
    this.hosts().forEach(h => this.live.unsubscribe(h.id));
  }

  cpuPercent(m?: LiveMetric): number {
    return m ? Math.round(m.cpuPercent) : 0;
  }

  memPercent(m?: LiveMetric): number {
    if (!m || !m.memoryTotalBytes) return 0;
    return Math.round((m.memoryUsedBytes / m.memoryTotalBytes) * 100);
  }

  diskPercent(total: number, used: number): number {
    return total ? Math.round((used / total) * 100) : 0;
  }

  isOnline(h: Host): boolean {
    const m = this.metrics()[h.id];
    const ts = m?.timestampUtc ?? h.lastSeenUtc;
    return this.now() - parseUtc(ts) < 30_000;
  }

  // AG Grid list view
  defaultColDef: ColDef = { sortable: true, resizable: true, filter: true, flex: 1 };

  gridCols: ColDef[] = [
    { headerName: 'Host', field: 'machineName', cellRenderer: (p: any) => `<a href="/hosts/${p.data.id}">${p.value}</a>` },
    { headerName: 'OS', field: 'operatingSystem' },
    { headerName: 'Status', valueGetter: (p: any) => this.isOnline(p.data) ? 'Online' : 'Stale' },
    { headerName: 'CPU %', valueGetter: (p: any) => this.cpuPercent(this.metrics()[p.data.id]) },
    {
      headerName: 'RAM %',
      valueGetter: (p: any) => this.memPercent(this.metrics()[p.data.id]),
      valueFormatter: (p: any) => `${p.value}%`
    },
    {
      headerName: 'RAM Used',
      valueGetter: (p: any) => this.metrics()[p.data.id]?.memoryUsedBytes ?? 0,
      valueFormatter: (p: any) => {
        const bytes = p.value as number;
        if (!bytes) return '—';
        const gb = bytes / 1073741824;
        return gb >= 1 ? `${gb.toFixed(1)} GB` : `${(bytes / 1048576).toFixed(0)} MB`;
      }
    },
    { headerName: 'Processes', valueGetter: (p: any) => this.processCounts()[p.data.id] ?? '—' },
  ];

  get gridRowData() {
    return this.filteredHosts();
  }
}

function parseUtc(ts: string): number {
  return new Date(ts.endsWith('Z') ? ts : ts + 'Z').getTime();
}
