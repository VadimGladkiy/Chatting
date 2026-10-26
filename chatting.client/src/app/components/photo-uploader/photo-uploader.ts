import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-photo-uploader',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-uploader.html',
  styleUrls: ['./photo-uploader.css']
})

export class PhotoUploaderComponent {
  previewUrl: string | null = null;

  // Событие для передачи Base64-строки родителю
  @Output() onPhotoReady = new EventEmitter<string>();

  onFileSelected(event: Event): void {
  const input = event.target as HTMLInputElement;
  if (!input.files || input.files.length === 0) return;

  const file = input.files[0]; // Важно: берем именно первый элемент [0]
  const reader = new FileReader();

  reader.onload = (e: any) => {
    const img = new Image();
    img.src = e.target.result;

    // Используем стрелочную функцию, чтобы "this" внутри указывал на компонент
    img.onload = () => {
      const maxWidth = 320;
      const maxHeight = 240;

      console.log(`Размеры исходного фото: ${img.width}x${img.height}`);

      if (img.width > maxWidth || img.height > maxHeight) {
        console.log('Картинка большая, запускаем resizeImage...');
        this.resizeImage(img, maxWidth, maxHeight);
      } else {
        console.log('Картинка маленькая, отправляем сразу...');
        this.emitPhoto(img.src);
      }
    };
  };

  reader.readAsDataURL(file);
}

  private resizeImage(img: HTMLImageElement, maxWidth: number, maxHeight: number): void {
    const canvas = document.createElement('canvas');
    let width = img.width;
    let height = img.height;

    if (width > height) {
      if (width > maxWidth) {
        height = Math.round((height * maxWidth) / width);
        width = maxWidth;
      }
    } else {
      if (height > maxHeight) {
        width = Math.round((width * maxHeight) / height);
        height = maxHeight;
      }
    }

    canvas.width = width;
    canvas.height = height;

    const ctx = canvas.getContext('2d');
    if (ctx) {
      ctx.drawImage(img, 0, 0, width, height);
      const resizedBase64 = canvas.toDataURL('image/png');
      this.emitPhoto(resizedBase64);
    }
  }

  private emitPhoto(base64Data: string): void {
    console.log('1. Дочерний компонент подготовил Base64 и сейчас вызовет emit()');
    this.previewUrl = base64Data;
    this.onPhotoReady.emit(base64Data); // Передаем родителю
  }
}

