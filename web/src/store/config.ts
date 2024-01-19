const env = process.env.NODE_ENV
const API_URL = env === 'development' ? 'http://localhost:5048/' : ''


export default {
    API_URL
}