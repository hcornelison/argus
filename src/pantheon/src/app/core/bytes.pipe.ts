import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'bytes', standalone: true })
export class BytesPipe implements PipeTransform {
  transform(value: number | null | undefined, digits = 1): string {
    if (value == null || isNaN(value)) return '—';
    if (value === 0) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.floor(Math.log(value) / Math.log(1024));
    return `${(value / Math.pow(1024, i)).toFixed(digits)} ${units[i]}`;
  }
}
