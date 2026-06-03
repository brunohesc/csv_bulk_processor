import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product, ProductFilter, PaginatedResult } from '../models/product.model';
import { UploadResponse } from '../models/import.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Import endpoints
  uploadFile(file: File): Observable<UploadResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<UploadResponse>(`${this.baseUrl}/import/upload`, formData);
  }

  cancelImport(jobId: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/import/${jobId}/cancel`, {});
  }

  // Product endpoints
  getProducts(filter: ProductFilter): Observable<PaginatedResult<Product>> {
    let params = new HttpParams();

    if (filter.importJobId) params = params.set('importJobId', filter.importJobId);
    if (filter.nameFilter) params = params.set('nameFilter', filter.nameFilter);
    if (filter.minPrice !== undefined) params = params.set('minPrice', filter.minPrice.toString());
    if (filter.maxPrice !== undefined) params = params.set('maxPrice', filter.maxPrice.toString());
    if (filter.expirationFrom) params = params.set('expirationFrom', filter.expirationFrom);
    if (filter.expirationTo) params = params.set('expirationTo', filter.expirationTo);
    if (filter.sortBy) params = params.set('sortBy', filter.sortBy);
    if (filter.sortOrder) params = params.set('sortOrder', filter.sortOrder);
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PaginatedResult<Product>>(`${this.baseUrl}/products`, { params });
  }

  getProduct(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.baseUrl}/products/${id}`);
  }
}
