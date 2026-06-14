import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef } from 'ag-grid-community';
import { KnobModule } from 'primeng/knob';
import { ProgressBarModule } from 'primeng/progressbar';
import { CardModule } from 'primeng/card';
import { ArgusApiService } from '../core/argus-api.service';
import { LiveService } from '../core/live.service';
import { BytesPipe } from '../core/bytes.pipe';
import { LiveMetric, ProcessInfo } from '../core/models';

@Component({
  selector: 'app-host-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, AgGridAngular, KnobModule, ProgressBarModule, CardModule, BytesPipe],
  templateUrl: './host-detail.component.html'
})
export class HostDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  private subs: Subscription[] = [];

  hostId = 0;
  metric = signal<LiveMetric | undefined>(undefined);
  processes = signal<ProcessInfo[]>([]);
  snapshotAt = signal<string | null>(null);

  defaultColDef: ColDef = { sortable: true, resizable: true, filter: true, flex: 1 };

  processCols: ColDef<ProcessInfo>[] = [
    { field: 'pid', headerName: 'PID', flex: 0, width: 80 },
    { field: 'name', headerName: 'Name', flex: 2 },
    { field: 'cpuPercent', headerName: 'CPU %', valueFormatter: p => p.value?.toFixed(1) ?? '0.0', sort: 'desc' },
    { field: 'memoryBytes', headerName: 'Memory', valueFormatter: p => formatBytes(p.value) },
    { field: 'threadCount', headerName: 'Threads', flex: 0, width: 100 },
  ];

  ngOnInit(): void {
    this.hostId = Number(this.route.snapshot.paramMap.get('id'));

    this.api.getProcesses(this.hostId).subscribe(s => {
      this.processes.set(s.processes);
      this.snapshotAt.set(s.timestampUtc);
    });

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

  memPercent(m?: LiveMetric): number {
    if (!m || !m.memoryTotalBytes) return 0;
    return Math.round((m.memoryUsedBytes / m.memoryTotalBytes) * 100);
  }
}

function formatBytes(bytes: number): string {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}
