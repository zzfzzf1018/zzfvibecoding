import axios from 'axios';

const instance = axios.create({
    baseURL: 'http://localhost:5000/api',
    timeout: 60000,
    headers: {
        'Content-Type': 'application/json',
    }
});

instance.interceptors.request.use(
    (config) => {
        console.log(`[AXIOS] Request: ${config.method?.toUpperCase()} ${config.url}`);
        return config;
    },
    (error) => {
        console.error(`[AXIOS] Request error: ${error.message}`);
        return Promise.reject(error);
    }
);

instance.interceptors.response.use(
    (response) => {
        console.log(`[AXIOS] Response: ${response.status} ${response.config.url}`);
        return response;
    },
    (error) => {
        console.error(`[AXIOS] Response error: ${error.message}`);
        if (error.response) {
            console.error(`[AXIOS] Status: ${error.response.status}, Data:`, error.response.data);
        }
        return Promise.reject(error);
    }
);

export default instance;