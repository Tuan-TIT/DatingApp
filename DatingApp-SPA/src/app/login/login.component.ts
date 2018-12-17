import { Component, OnInit } from '@angular/core';
import { AuthService } from '../_services/auth.service';
import { AlertifyService } from '../_services/alertify.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {

  model: any = {};
  registerMode = false;

  constructor(private authService: AuthService, private alertify: AlertifyService,
    private routes: Router) { }

  ngOnInit() {
  }

  login() {
    this.authService.login(this.model).subscribe(next => {
      this.alertify.success('login successfully');
    }, error => {
      this.alertify.error(error);
    }, () => {
      this.routes.navigate(['/members']);
    });
  }

  loggedIn() {
    return this.authService.loggedIn();
  }

  cancelRegisterMode(registerMode: boolean) {
    this.registerMode = registerMode;
  }
}
