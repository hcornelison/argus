import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, ValueFormatterParams } from 'ag-grid-community';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ArgusApiService } from '../core/argus-api.service';
import { LiveService } from '../core/live.service';
import { ThemeService } from '../core/theme.service';
import { EventLog, Host } from '../core/models';

@Component({
  selector: 'app-events',
  standalone: true,
  imports: [CommonModule, FormsModule, AgGridAngular, SelectModule, InputTextModule, ButtonModule, ToggleSwitchModule],
  templateUrl: './events.component.html'
})
export class EventsComponent implements OnInit, OnDestroy {
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  readonly theme = inject(ThemeService);
  private evtSub?: Subscription;

  hosts = signal<Host[]>([]);
  hostNames = signal<Record<number, string>>({});
  entries = signal<EventLog[]>([]);

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

  defaultColDef: ColDef = { sortable: true, resizable: true, filter: true };

  eventCols: ColDef<EventLog>[] = [
    {
      field: 'timestampUtc', headerName: 'Time', width: 190, sort: 'desc',
      valueFormatter: (p: ValueFormatterParams) => p.value ? new Date(p.value).toLocaleString() : ''
    },
    {
      headerName: 'Host', width: 130,
      valueGetter: p => this.hostNames()[p.data?.hostId ?? 0] ?? (p.data?.hostId ?? '')
    },
    { field: 'level', headerName: 'Level', width: 110 },
    { field: 'channel', headerName: 'Channel', width: 170 },
    { field: 'source', headerName: 'Source', width: 150 },
    { field: 'eventId', headerName: 'ID', width: 70 },
    { field: 'message', headerName: 'Message', flex: 1, minWidth: 200, wrapText: true, autoHeight: true },
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
}
