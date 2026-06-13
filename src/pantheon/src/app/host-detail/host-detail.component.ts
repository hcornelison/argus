import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { TableModule } from 'primeng/table';
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
  imports: [CommonModule, FormsModule, RouterLink, TableModule, KnobModule, ProgressBarModule, CardModule, BytesPipe],
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
