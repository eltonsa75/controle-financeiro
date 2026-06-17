import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'sort',
  standalone: true // Isso é essencial por causa da sua estrutura
})
export class SortPipe implements PipeTransform {
  transform(array: any[], field: string): any[] {
    if (!array) return [];
    
    return [...array].sort((a, b) => {
      const valA = a[field].toLowerCase();
      const valB = b[field].toLowerCase();
      return valA.localeCompare(valB);
    });
  }
}