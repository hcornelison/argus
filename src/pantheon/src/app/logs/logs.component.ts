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
import { Host, LogLine } from '../core/models';

@Component({
  selector: 'app-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, AgGridAngular, SelectModule, InputTextModule, ButtonModule, ToggleSwitchModule],
  templateUrl: './logs.component.html'
})
export class LogsComponent implements OnInit, OnDestroy {
  private api = inject(ArgusApiService);
  private live = inject(LiveService);
  readonly theme = inject(ThemeService);
  private logSub?: Subscription;

  hosts = signal<Host[]>([]);
  files = signal<string[]>([]);
  lines = signal<LogLine[]>([]);

  hostId: number | null = null;
  filePath: string | null = null;
  search = '';
  minutes = 5;
  liveTail = false;

  windowOptions = [
    { label: 'Last 5 min', value: 5 },
    { label: 'Last 15 min', value: 15 },
    { label: 'Last hour', value: 60 },
    { label: 'Last 24 hours', value: 1440 }
  ];

  defaultColDef: ColDef = { sortable: true, resizable: true, filter: true };

  logCols: ColDef<LogLine>[] = [
    {
      field: 'timestampUtc', headerName: 'Time', width: 190, sort: 'desc',
      valueFormatter: (p: ValueFormatterParams) => p.value ? new Date(p.value).toLocaleString() : ''
    },
    { field: 'filePath', headerName: 'File', width: 260, cellClass: 'mono-cell' },
    { field: 'line', headerName: 'Line', flex: 1, minWidth: 200, cellClass: 'mono-cell', wrapText: true, autoHeight: true },
  ];

  ngOnInit(): void {
    this.api.getHosts().subscribe(h => this.hosts.set(h));
    this.refreshFiles();
    this.search_();
  }

  ngOnDestroy(): void {
    this.stopLiveTail();
  }

  onHostChange(): void {
    this.filePath = null;
    this.refreshFiles();
    this.search_();
  }

  refreshFiles(): void {
    this.api.getLogFiles(this.hostId ?? undefined).subscribe(f => this.files.set(f));
  }

  search_(): void {
    const to = new Date();
    const from = new Date(to.getTime() - this.minutes * 60_000);
    this.api.getLogs({
      hostId: this.hostId ?? undefined,
      filePath: this.filePath ?? undefined,
      from, to,
      q: this.search || undefined
    }).subscribe(lines => this.lines.set(lines));
  }

  toggleLiveTail(): void {
    if (this.liveTail) this.startLiveTail();
    else this.stopLiveTail();
  }

  private async startLiveTail(): Promise<void> {
    this.logSub = this.live.logs.subscribe(evt => {
      if (this.hostId != null && evt.hostId !== this.hostId) return;
      const incoming = evt.lines.filter(l =>
        (!this.filePath || l.filePath === this.filePath) &&
        (!this.search || l.line.toLowerCase().includes(this.search.toLowerCase())));
      if (incoming.length) this.lines.update(cur => [...incoming.reverse(), ...cur].slice(0, 2000));
    });
    const ids = this.hostId != null ? [this.hostId] : this.hosts().map(h => h.id);
    for (const id of ids) await this.live.subscribe(id);
  }

  private stopLiveTail(): void {
    this.logSub?.unsubscribe();
    this.logSub = undefined;
  }
}
