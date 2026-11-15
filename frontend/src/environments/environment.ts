export const environment = {
  production: false, // Alterei para false por ser ambiente de desenvolvimento local
  firebase: {
    apiKey: "AIzaSyDMX3RdT1woyGYYJ_MUvqkZSMKKVh9UzeQ",
    authDomain: "controle-financeiro-81997.firebaseapp.com",
    projectId: "controle-financeiro-81997",
    storageBucket: "controle-financeiro-81997.firebasestorage.app",
    messagingSenderId: "277353136195",
    appId: "1:277353136195:web:a8b8c028410b9384d614f6",
    measurementId: "G-V98M4TVCYM"
  },
  // Altere para a porta 8080 que o seu Docker está expondo
  apiUrl: 'http://localhost:8080/api'
};