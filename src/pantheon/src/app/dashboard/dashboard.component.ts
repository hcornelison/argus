import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { CardModule } from 'primeng/card';
import { KnobModule } from 'primeng/knob';
import { ProgressBarModule } from 'primeng/progressbar';
import { TagModule } from 'primeng/tag';
import { ArgusApiService } from '../core/argus-api.service';
import { LiveService } from '../core/live.service';
import { BytesPipe } from '../core/bytes.pipe';
import { Host, LiveMetric } from '../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CardModule, KnobModule, ProgressBarModule, TagModule, BytesPipe],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit, OnDestroy {
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  private sub?: Subscription;

  hosts = signal<Host[]>([]);
  metrics = signal<Record<number, LiveMetric>>({});

  ngOnInit(): void {
    this.api.getHosts().subscribe(async hosts => {
      this.hosts.set(hosts);
      this.sub = this.live.metric.subscribe(m =>
        this.metrics.update(cur => ({ ...cur, [m.hostId]: m })));
      for (const h of hosts) await this.live.subscribe(h.id);
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.hosts().forEach(h => this.live.unsubscribe(h.id));
  }

  memPercent(m?: LiveMetric): number {
    if (!m || !m.memoryTotalBytes) return 0;
    return Math.round((m.memoryUsedBytes / m.memoryTotalBytes) * 100);
  }

  diskPercent(total: number, used: number): number {
    return total ? Math.round((used / total) * 100) : 0;
  }

  isOnline(h: Host): boolean {
    return Date.now() - new Date(h.lastSeenUtc).getTime() < 60_000;
  }
}
