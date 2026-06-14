export interface Host {
  id: number;
  machineName: string;
  operatingSystem: string;
  agentVersion: string;
  firstSeenUtc: string;
  lastSeenUtc: string;
}

export interface DiskUsage {
  mount: string;
  totalBytes: number;
  usedBytes: number;
}

export interface MetricPoint {
  timestampUtc: string;
  cpuPercent: number;
  memoryTotalBytes: number;
  memoryUsedBytes: number;
}

export interface DiskPoint extends DiskUsage {
  timestampUtc: string;
}

export interface MetricsResponse {
  metrics: MetricPoint[];
  disks: DiskPoint[];
}

/** Live metric pushed over SignalR. */
export interface LiveMetric {
  hostId: number;
  timestampUtc: string;
  cpuPercent: number;
  memoryTotalBytes: number;
  memoryUsedBytes: number;
  disks: DiskUsage[];
}

export interface ProcessInfo {
  pid: number;
  name: string;
  cpuPercent: number;
  memoryBytes: number;
  threadCount: number;
}

export interface ProcessSnapshot {
  timestampUtc: string | null;
  processes: ProcessInfo[];
}

export interface LiveProcesses {
  hostId: number;
  timestampUtc: string;
  processes: ProcessInfo[];
}

export interface ProcessHistoryPoint {
  timestampUtc: string;
  cpuPercent: number;
  memoryBytes: number;
  threadCount: number;
}

export interface LogLine {
  hostId: number;
  timestampUtc: string;
  filePath: string;
  line: string;
  level: string;
}

export interface LiveLogs {
  hostId: number;
  lines: LogLine[];
}

export interface EventLog {
  hostId?: number;
  timestampUtc: string;
  channel: string;
  source: string;
  level: string;
  eventId: number;
  message: string;
}

export interface LiveEvents {
  hostId: number;
  entries: EventLog[];
}
