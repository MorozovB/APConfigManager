import axios from 'axios';

const apiClient = axios.create({

  baseURL: '/api',

  timeout: 120000,

  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.response.use(
  (response) => response,


  (error) => {

    const message = error.response?.data?.message
      || error.response?.data
      || error.message
      || 'Unknown error';

    console.error(`API Error [${error.config?.method?.toUpperCase()} ${error.config?.url}]:`, message);

    return Promise.reject(error);
  }
);

export default apiClient;
