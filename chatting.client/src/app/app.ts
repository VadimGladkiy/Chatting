import { Component } from '@angular/core';
import { MainPageComponent } from './components/main-page/main-page';


@Component({
  selector: 'app-root',
  standalone: true,
  styleUrl: './app.css',
  templateUrl: './app.html',
  imports: [MainPageComponent]
})
export class AppComponent {

}
