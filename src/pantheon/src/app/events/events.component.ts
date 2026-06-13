import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { TableModule } from 'primeng/table';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { ArgusApiService } from '../core/argus-api.service';
import { LiveService } from '../core/live.service';
import { EventLog, Host } from '../core/models';

@Component({
  selector: 'app-events',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, SelectModule, InputTextModule, ButtonModule, ToggleSwitchModule, TagModule],
  templateUrl: './events.component.html'
})
export class EventsComponent implements OnInit, OnDestroy {
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  private evtSub?: Subscription;

  hosts = signal<Host[]>([]);
  hostNames = signal<Record<number, string>>({});
  entries = signal<EventLog[]>([]);

  // Filters
  hostId: number | null = null;
  level: string | null = null;
  search = '';
  minutes = 60;
  liveTail = false;

  levelOptions = [
    { label: 'Critical', value: 'Critical' },
    { label: 'Error', value: 'Error' },
    { label: 'Warning', value: 'Warning' },
    { label: 'Information', value: 'Information' }
  ];

  windowOptions = [
    { label: 'Last 15 min', value: 15 },
    { label: 'Last hour', value: 60 },
    { label: 'Last 6 hours', value: 360 },
    { label: 'Last 24 hours', value: 1440 }
  ];

  ngOnInit(): void {
    this.api.getHosts().subscribe(h => {
      this.hosts.set(h);
      this.hostNames.set(Object.fromEntries(h.map(x => [x.id, x.machineName])));
    });
    this.search_();
  }

  ngOnDestroy(): void {
    this.stopLiveTail();
  }

  search_(): void {
    const to = new Date();
    const from = new Date(to.getTime() - this.minutes * 60_000);
    this.api.getEvents({
      hostId: this.hostId ?? undefined,
      level: this.level ?? undefined,
      from, to,
      q: this.search || undefined
    }).subscribe(e => this.entries.set(e));
  }

  toggleLiveTail(): void {
    if (this.liveTail) this.startLiveTail();
    else this.stopLiveTail();
  }

  private async startLiveTail(): Promise<void> {
    this.evtSub = this.live.events.subscribe(evt => {
      if (this.hostId != null && evt.hostId !== this.hostId) return;
      const incoming = evt.entries
        .map(e => ({ ...e, hostId: evt.hostId }))
        .filter(e =>
          (!this.level || e.level === this.level) &&
          (!this.search || e.message.toLowerCase().includes(this.search.toLowerCase())));
      if (incoming.length) this.entries.update(cur => [...incoming.reverse(), ...cur].slice(0, 2000));
    });
    const ids = this.hostId != null ? [this.hostId] : this.hosts().map(h => h.id);
    for (const id of ids) await this.live.subscribe(id);
  }

  private stopLiveTail(): void {
    this.evtSub?.unsubscribe();
    this.evtSub = undefined;
  }

  severity(level: string): string {
    switch (level) {
      case 'Critical':
      case 'Error': return 'danger';
      case 'Warning': return 'warn';
      case 'Information': return 'info';
      default: return 'secondary';
    }
  }
}
